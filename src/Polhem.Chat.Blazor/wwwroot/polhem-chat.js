// The browser side of Polhem.Chat.Blazor's ChatView.

const watched = new WeakMap();
const followers = new WeakMap();
const prompts = new WeakMap();

// How close to the end, in CSS pixels, still counts as being at the end. Without some slack a fractional offset would
// make the view stop following a reply it never left.
const endTolerance = 4;

// Keeps the newest entry in sight as the content grows, until the view leaves the end by scrolling up; tells the view
// (bridge) when it leaves the end or comes back to it. A streaming reply is followed here, without a round trip to the
// server per token.
//
// NOTE: Any upward move that leaves the end stops following, whoever made it. In a browser the user moves the offset in
// many ways besides the wheel and the scroll bar: moving the keyboard focus to an earlier reply, finding text in the page,
// a screen reader's cursor, a link to an anchor. Pulling the view back from any of those would leave keyboard and screen
// reader users unable to read back. A move the browser makes itself, clamping the offset when the content shrinks,
// stays at the end and changes nothing.
export function follow(scroller, bridge) {
    if (followers.has(scroller)) {
        return;
    }
    const state = { follows: true, lastTop: scroller.scrollTop };
    const atEnd = () => scroller.scrollTop + scroller.clientHeight >= scroller.scrollHeight - endTolerance;
    const toEnd = () => {
        scroller.scrollTop = scroller.scrollHeight;
        state.lastTop = scroller.scrollTop;
    };
    const set = (follows) => {
        if (state.follows !== follows) {
            state.follows = follows;
            // The view may be gone, or its circuit; there is no one left to tell then.
            bridge.invokeMethodAsync('OnFollowsLatestChanged', follows).catch(() => { });
        }
    };
    const onScroll = () => {
        const top = scroller.scrollTop;
        const up = top < state.lastTop;
        state.lastTop = top;
        if (up || !state.follows) {
            set(atEnd());
        }
    };
    // The content grew, or the box changed: a view that follows goes back to the end, and one that does not follows
    // again once the content fits or ends in sight (after a clear or a rewind).
    const grow = new ResizeObserver(() => {
        if (!scroller.isConnected) {
            unfollow(scroller);
        } else if (state.follows) {
            toEnd();
        } else if (atEnd()) {
            set(true);
        }
    });
    for (const child of scroller.children) {
        grow.observe(child);
    }
    grow.observe(scroller);
    scroller.addEventListener('scroll', onScroll, { passive: true });
    followers.set(scroller, { state, grow, onScroll, toEnd });
    toEnd();
}

// Scrolls to the newest entry and follows new entries again; the view already knows. With focus, the keyboard focus moves
// to the conversation, for a button that goes away once it has done its work.
export function scrollToLatest(scroller, focus) {
    const follower = followers.get(scroller);
    if (follower) {
        follower.state.follows = true;
        follower.toEnd();
    } else {
        scroller.scrollTop = scroller.scrollHeight;
    }
    if (focus) {
        scroller.focus({ preventScroll: true });
    }
}

export function unfollow(scroller) {
    const follower = scroller && followers.get(scroller);
    if (follower) {
        follower.grow.disconnect();
        scroller.removeEventListener('scroll', follower.onScroll);
        followers.delete(scroller);
    }
}

// Copies text to the clipboard. The browser allows it only in a secure context (HTTPS or localhost) and may refuse it;
// the view then has nothing else to do, so a refusal is reported as false rather than as an error.
export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

// Marks each approval card whose arguments are taller than their box with data-overflowing, which shows the note that
// there is more to read. What the user approves is what the tool receives, so they must know when part of it is hidden.
export function watchOverflow(root) {
    if (watched.has(root)) {
        return;
    }
    const update = () => {
        if (!root.isConnected) {
            unwatch(root);
            return;
        }
        for (const box of root.querySelectorAll('.pch-approval-args')) {
            const card = box.parentElement;
            if (box.scrollHeight > box.clientHeight + 1) {
                card.dataset.overflowing = '';
            } else {
                delete card.dataset.overflowing;
            }
        }
    };
    const resize = new ResizeObserver(update);
    const observed = new Set();
    // Only the boxes come and go; a streaming reply changes text, which the observer is not asked about. A box that
    // appears is observed, and the resize observer then reports it at once.
    const track = () => {
        for (const box of observed) {
            if (!box.isConnected) {
                resize.unobserve(box);
                observed.delete(box);
            }
        }
        for (const box of root.querySelectorAll('.pch-approval-args')) {
            if (!observed.has(box)) {
                resize.observe(box);
                observed.add(box);
            }
        }
    };
    const mutation = new MutationObserver(track);
    mutation.observe(root, { childList: true, subtree: true });
    resize.observe(root);
    watched.set(root, { resize, mutation });
    track();
    update();
}

export function unwatch(root) {
    const observers = root && watched.get(root);
    if (observers) {
        observers.resize.disconnect();
        observers.mutation.disconnect();
        watched.delete(root);
    }
}

// The keys and the files of the prompt box: Enter sends what the box holds, Shift+Enter starts a new line, and Esc
// stops the reply or ends the edit while the view marks either (data-generating, data-editing). While the view marks the
// list of commands open (data-commands), the arrows move its selection and Esc closes it; Enter still hands over the
// text, and the view chooses the command when the text opens the list. Files dropped on the view, or a picture pasted in the box,
// go to the view's own file input, the one the attach button opens, so they are checked like the files the user picks.
//
// WARNING: An input method (Chinese, Japanese, Korean) confirms its composition with Enter and cancels it with Esc. Those
// keys must reach the input method, never send or stop: isComposing tells it, and some browsers report it only as key
// code 229.
export function watchPrompt(root, box, bridge) {
    if (prompts.has(root)) {
        return;
    }
    const tell = (method, ...args) => bridge.invokeMethodAsync(method, ...args).catch(() => { });
    const onKeyDown = (event) => {
        if (event.isComposing || event.keyCode === 229) {
            return;
        }
        const plain = !event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey;
        if ('commands' in root.dataset && event.target === box && plain
            && (event.key === 'ArrowUp' || event.key === 'ArrowDown' || event.key === 'Escape')) {
            event.preventDefault();
            event.stopPropagation();
            if (event.key === 'Escape') {
                tell('CloseCommands');
            } else {
                tell('MoveCommand', event.key === 'ArrowUp' ? -1 : 1);
            }
            return;
        }
        if (event.key === 'Escape') {
            if ('generating' in root.dataset || 'editing' in root.dataset) {
                event.preventDefault();
                event.stopPropagation();
                tell('Escape');
            }
        } else if (event.key === 'Enter' && event.target === box && plain) {
            event.preventDefault();
            tell('Submit', box.value);
        }
    };
    // Only while the view takes files; otherwise the page decides what a drop does.
    const fileInput = () => root.querySelector('.pch-file-input');
    const carriesFiles = (transfer) => Array.from(transfer?.types ?? []).includes('Files');
    const take = (files) => {
        const input = fileInput();
        if (!input || !files || files.length === 0) {
            return false;
        }
        input.files = files;
        input.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
    };
    const onDragOver = (event) => {
        if (carriesFiles(event.dataTransfer) && fileInput()) {
            event.preventDefault();
            event.dataTransfer.dropEffect = 'copy';
            root.dataset.dropping = '';
        }
    };
    const onDragLeave = (event) => {
        if (!event.relatedTarget || !root.contains(event.relatedTarget)) {
            delete root.dataset.dropping;
        }
    };
    const onDrop = (event) => {
        delete root.dataset.dropping;
        if (take(event.dataTransfer?.files)) {
            event.preventDefault();
        }
    };
    const onPaste = (event) => {
        // Text pastes as text, even when the clipboard carries a picture of it too, as a copy from a spreadsheet does;
        // only files without text, such as a copied picture, are attached.
        const data = event.clipboardData;
        if (data?.getData?.('text/plain') || data?.getData?.('text/html')) {
            return;
        }
        if (data?.files?.length > 0 && take(data.files)) {
            event.preventDefault();
        }
    };
    // The selected command stays in sight in a list taller than its box, however the selection moved.
    const reveal = new MutationObserver((records) => {
        for (const record of records) {
            if (record.target.getAttribute?.('aria-selected') === 'true') {
                record.target.scrollIntoView?.({ block: 'nearest' });
            }
        }
    });
    reveal.observe(root, { subtree: true, attributes: true, attributeFilter: ['aria-selected'] });
    root.addEventListener('keydown', onKeyDown);
    root.addEventListener('dragover', onDragOver);
    root.addEventListener('dragleave', onDragLeave);
    root.addEventListener('drop', onDrop);
    box.addEventListener('paste', onPaste);
    prompts.set(root, { box, reveal, onKeyDown, onDragOver, onDragLeave, onDrop, onPaste });
}

export function unwatchPrompt(root) {
    const prompt = root && prompts.get(root);
    if (prompt) {
        prompt.reveal.disconnect();
        root.removeEventListener('keydown', prompt.onKeyDown);
        root.removeEventListener('dragover', prompt.onDragOver);
        root.removeEventListener('dragleave', prompt.onDragLeave);
        root.removeEventListener('drop', prompt.onDrop);
        prompt.box.removeEventListener('paste', prompt.onPaste);
        prompts.delete(root);
    }
}
