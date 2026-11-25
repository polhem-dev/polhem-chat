// The browser side of Polhem.Chat.Blazor's ChatView.

const watched = new WeakMap();
const followers = new WeakMap();

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
