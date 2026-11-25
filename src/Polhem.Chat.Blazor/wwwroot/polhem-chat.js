// The browser side of Polhem.Chat.Blazor's ChatView.

const watched = new WeakMap();
const followers = new WeakMap();

// How close to the end, in CSS pixels, still counts as being at the end. Without some slack a fractional offset would
// make the view stop following a reply it never left.
const endTolerance = 4;

// How long after the user's last scroll input a move of the offset still counts as theirs. A wheel or trackpad keeps
// sending events while it moves, so this only has to bridge the gap between two of them.
const userScrollWindow = 400;

const scrollKeys = new Set(['ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' ']);

// Keeps the newest entry in sight as the content grows, until the user scrolls up; tells the view (bridge) when the user
// leaves the end or comes back to it. A streaming reply is followed here, without a round trip to the server per token.
export function follow(scroller, bridge) {
    if (followers.has(scroller)) {
        return;
    }
    const state = { follows: true, lastInput: -Infinity, lastTop: scroller.scrollTop };
    const atEnd = () => scroller.scrollTop + scroller.clientHeight >= scroller.scrollHeight - endTolerance;
    const toEnd = () => {
        scroller.scrollTop = scroller.scrollHeight;
        state.lastTop = scroller.scrollTop;
    };
    const set = (follows) => {
        if (state.follows !== follows) {
            state.follows = follows;
            bridge.invokeMethodAsync('OnFollowsLatestChanged', follows);
        }
    };
    const input = () => { state.lastInput = performance.now(); };
    // A press on the scroll bar targets the scroller itself; a press on the content (a button, a card) is not a scroll.
    const pointerInput = (event) => {
        if (event.target === scroller) {
            input();
        }
    };
    const keyInput = (event) => {
        if (scrollKeys.has(event.key)) {
            input();
        }
    };
    // NOTE: The offset also moves without the user: the browser clamps it when the content shrinks, and an element that
    // takes focus scrolls itself into view, often far above the end. So only an offset that went up while the user was
    // scrolling leaves the end; any other move is followed back to it.
    const onScroll = () => {
        const top = scroller.scrollTop;
        const up = top < state.lastTop;
        state.lastTop = top;
        if (up && performance.now() - state.lastInput < userScrollWindow) {
            set(atEnd());
        } else if (state.follows) {
            if (!atEnd()) {
                toEnd();
            }
        } else if (!up && atEnd()) {
            set(true);
        }
    };
    const grow = new ResizeObserver(() => {
        if (state.follows) {
            toEnd();
        }
    });
    for (const child of scroller.children) {
        grow.observe(child);
    }
    grow.observe(scroller);
    const listeners = [
        ['scroll', onScroll],
        ['wheel', input],
        ['touchmove', input],
        ['pointerdown', pointerInput],
        ['keydown', keyInput],
    ];
    for (const [name, handler] of listeners) {
        scroller.addEventListener(name, handler, { passive: true });
    }
    followers.set(scroller, { state, grow, listeners, toEnd });
    toEnd();
}

// Scrolls to the newest entry and follows new entries again; the view already knows.
export function scrollToLatest(scroller) {
    const follower = followers.get(scroller);
    if (follower) {
        follower.state.follows = true;
        follower.toEnd();
    } else {
        scroller.scrollTop = scroller.scrollHeight;
    }
}

export function unfollow(scroller) {
    const follower = followers.get(scroller);
    if (follower) {
        follower.grow.disconnect();
        for (const [name, handler] of follower.listeners) {
            scroller.removeEventListener(name, handler);
        }
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
    const observers = watched.get(root);
    if (observers) {
        observers.resize.disconnect();
        observers.mutation.disconnect();
        watched.delete(root);
    }
}
