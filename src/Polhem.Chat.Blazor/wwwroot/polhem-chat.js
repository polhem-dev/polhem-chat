// The browser side of Polhem.Chat.Blazor's ChatView.

const watched = new WeakMap();

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
                card.setAttribute('data-overflowing', '');
            } else {
                card.removeAttribute('data-overflowing');
            }
        }
    };
    const resize = new ResizeObserver(update);
    const mutation = new MutationObserver(() => {
        for (const box of root.querySelectorAll('.pch-approval-args')) {
            resize.observe(box);
        }
        update();
    });
    mutation.observe(root, { childList: true, subtree: true, characterData: true });
    resize.observe(root);
    watched.set(root, { resize, mutation });
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
