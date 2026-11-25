// Tests of src/Polhem.Chat.Blazor/wwwroot/polhem-chat.js, the browser side of the Blazor ChatView, run with Node's own
// test runner on a small stand-in for the DOM. No package is installed:
//
//   node --test tests/Polhem.Chat.Blazor.ScriptTests/polhem-chat.test.mjs
//
// The stand-in models what the script reads and sets: an element's scroll offset, which the browser clamps to its
// content, its children, its dataset, and the events and observers the script registers.

import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';

class FakeObserver {
    static instances = [];

    constructor(callback) {
        this.callback = callback;
        this.observed = new Set();
        this.disconnected = false;
        FakeObserver.instances.push(this);
    }

    observe(target) {
        this.observed.add(target);
    }

    unobserve(target) {
        this.observed.delete(target);
    }

    disconnect() {
        this.observed.clear();
        this.disconnected = true;
    }

    fire() {
        this.callback([], this);
    }
}

class ResizeObserver extends FakeObserver {}
class MutationObserver extends FakeObserver {}

globalThis.ResizeObserver = ResizeObserver;
globalThis.MutationObserver = MutationObserver;

const resizeObservers = () => FakeObserver.instances.filter((o) => o instanceof ResizeObserver);
const mutationObservers = () => FakeObserver.instances.filter((o) => o instanceof MutationObserver);

class FakeElement {
    constructor({ className = '', clientHeight = 0, scrollHeight = 0 } = {}) {
        this.className = className;
        this.clientHeight = clientHeight;
        this.scrollHeight = scrollHeight;
        this.children = [];
        this.parentElement = null;
        this.isConnected = true;
        this.dataset = {};
        this.listeners = new Map();
        this.focused = null;
        this._scrollTop = 0;
    }

    focus(options) {
        this.focused = options;
    }

    get scrollTop() {
        return this._scrollTop;
    }

    // The browser keeps the offset between 0 and the content's height minus the box's.
    set scrollTop(value) {
        this._scrollTop = Math.max(0, Math.min(value, Math.max(0, this.scrollHeight - this.clientHeight)));
    }

    append(child) {
        child.parentElement = this;
        this.children.push(child);
        return child;
    }

    remove(child) {
        this.children = this.children.filter((c) => c !== child);
        child.isConnected = false;
    }

    querySelectorAll(selector) {
        const name = selector.replace(/^\./, '');
        const found = [];
        const visit = (element) => {
            for (const child of element.children) {
                if (child.isConnected && child.className.split(' ').includes(name)) {
                    found.push(child);
                }
                visit(child);
            }
        };
        visit(this);
        return found;
    }

    addEventListener(name, handler) {
        this.listeners.set(name, [...(this.listeners.get(name) ?? []), handler]);
    }

    removeEventListener(name, handler) {
        this.listeners.set(name, (this.listeners.get(name) ?? []).filter((h) => h !== handler));
    }

    querySelector(selector) {
        return this.querySelectorAll(selector)[0] ?? null;
    }

    contains(element) {
        for (let current = element; current; current = current.parentElement) {
            if (current === this) {
                return true;
            }
        }
        return false;
    }

    // A real event the script raises on an element, such as the change event of the file input.
    dispatchEvent(event) {
        this.dispatched = [...(this.dispatched ?? []), event];
        return true;
    }

    dispatch(name, event = {}) {
        for (const handler of this.listeners.get(name) ?? []) {
            handler({ target: this, ...event });
        }
    }

    // Scrolls the way a user or the browser does: the offset moves, then the browser raises scroll.
    scrollTo(top) {
        this.scrollTop = top;
        this.dispatch('scroll');
    }
}

class FakeBridge {
    calls = [];

    constructor({ fails = false } = {}) {
        this.fails = fails;
    }

    invokeMethodAsync(method, ...args) {
        this.calls.push([method, ...args]);
        return this.fails ? Promise.reject(new Error('The circuit is gone.')) : Promise.resolve();
    }
}

const script = await import('../../src/Polhem.Chat.Blazor/wwwroot/polhem-chat.js');

beforeEach(() => {
    FakeObserver.instances = [];
});

function chat({ clientHeight = 400, scrollHeight = 1000 } = {}) {
    const scroller = new FakeElement({ className: 'pch-scroller', clientHeight, scrollHeight });
    scroller.append(new FakeElement({ className: 'pch-content' }));
    return scroller;
}

test('copyText reports whether the clipboard took the text', async () => {
    const copied = [];
    Object.defineProperty(globalThis.navigator, 'clipboard', {
        configurable: true,
        value: { writeText: async (text) => { copied.push(text); } },
    });
    assert.equal(await script.copyText('Hello'), true);
    assert.deepEqual(copied, ['Hello']);

    Object.defineProperty(globalThis.navigator, 'clipboard', {
        configurable: true,
        value: { writeText: async () => { throw new Error('Not allowed.'); } },
    });
    assert.equal(await script.copyText('Hello'), false);
});

test('an approval card is marked while its arguments are taller than their box, and only then', () => {
    const root = new FakeElement();
    const card = root.append(new FakeElement({ className: 'pch-approval' }));
    const box = card.append(new FakeElement({ className: 'pch-approval-args', clientHeight: 240, scrollHeight: 600 }));

    script.watchOverflow(root);
    assert.equal(card.dataset.overflowing, '');
    assert.ok(resizeObservers()[0].observed.has(box));

    box.scrollHeight = 200;
    resizeObservers()[0].fire();
    assert.equal('overflowing' in card.dataset, false);

    script.unwatch(root);
    assert.ok(FakeObserver.instances.every((o) => o.disconnected));
});

test('a card that appears is watched, one that goes is let go, and a root is watched once', () => {
    const root = new FakeElement();
    script.watchOverflow(root);
    script.watchOverflow(root);
    assert.equal(mutationObservers().length, 1);

    const card = root.append(new FakeElement({ className: 'pch-approval' }));
    const box = card.append(new FakeElement({ className: 'pch-approval-args', clientHeight: 240, scrollHeight: 500 }));
    mutationObservers()[0].fire();
    assert.ok(resizeObservers()[0].observed.has(box));

    root.remove(card);
    box.isConnected = false;
    mutationObservers()[0].fire();
    assert.equal(resizeObservers()[0].observed.has(box), false);

    script.unwatch(root);
    script.unwatch(root);
});

test('following starts at the end and keeps it in sight as the content grows', () => {
    const scroller = chat();
    script.follow(scroller, new FakeBridge());
    assert.equal(scroller.scrollTop, 600);

    scroller.scrollHeight = 1300;
    resizeObservers()[0].fire();
    assert.equal(scroller.scrollTop, 900);
    script.unfollow(scroller);
});

test('scrolling up stops following, whatever moved the view, and scrolling back to the end follows again', () => {
    const scroller = chat();
    const bridge = new FakeBridge();
    script.follow(scroller, bridge);

    // The wheel, the keyboard, a focused button of an earlier reply or finding text: all move the offset up.
    scroller.scrollTo(200);
    assert.deepEqual(bridge.calls, [['OnFollowsLatestChanged', false]]);

    scroller.scrollHeight = 1300;
    resizeObservers()[0].fire();
    assert.equal(scroller.scrollTop, 200);

    scroller.scrollTo(900);
    assert.deepEqual(bridge.calls.at(-1), ['OnFollowsLatestChanged', true]);
    script.unfollow(scroller);
});

test('the browser clamping the offset as the content shrinks keeps following', () => {
    const scroller = chat();
    const bridge = new FakeBridge();
    script.follow(scroller, bridge);

    scroller.scrollHeight = 700;
    scroller.scrollTo(scroller.scrollTop);

    assert.equal(scroller.scrollTop, 300);
    assert.deepEqual(bridge.calls, []);
    script.unfollow(scroller);
});

test('a view that stopped following follows again once its content fits, as after a clear', () => {
    const scroller = chat();
    const bridge = new FakeBridge();
    script.follow(scroller, bridge);
    scroller.scrollTo(0);
    assert.deepEqual(bridge.calls, [['OnFollowsLatestChanged', false]]);

    scroller.scrollHeight = 300;
    resizeObservers()[0].fire();

    assert.deepEqual(bridge.calls.at(-1), ['OnFollowsLatestChanged', true]);
    script.unfollow(scroller);
});

test('a view whose circuit went away does not fail when the user scrolls', async () => {
    const scroller = chat();
    const bridge = new FakeBridge({ fails: true });
    const unhandled = [];
    const record = (reason) => unhandled.push(reason);
    process.on('unhandledRejection', record);
    script.follow(scroller, bridge);

    scroller.scrollTo(0);
    await new Promise((resolve) => setImmediate(resolve));

    process.off('unhandledRejection', record);
    assert.deepEqual(bridge.calls, [['OnFollowsLatestChanged', false]]);
    assert.deepEqual(unhandled, []);

    script.unfollow(scroller);
});

test('a scroller taken out of the page stops being followed by itself', () => {
    const scroller = chat();
    script.follow(scroller, new FakeBridge());

    scroller.isConnected = false;
    resizeObservers()[0].fire();

    assert.ok(resizeObservers()[0].disconnected);
    assert.equal(scroller.listeners.get('scroll').length, 0);
});

test('scrolling to the latest entry follows again without telling the view, which asked for it', () => {
    const scroller = chat();
    const bridge = new FakeBridge();
    script.follow(scroller, bridge);
    scroller.scrollTo(0);
    bridge.calls.length = 0;

    script.scrollToLatest(scroller, false);
    assert.equal(scroller.scrollTop, 600);
    assert.equal(scroller.focused, null);
    scroller.scrollHeight = 1200;
    resizeObservers()[0].fire();
    assert.equal(scroller.scrollTop, 800);
    assert.deepEqual(bridge.calls, []);

    script.scrollToLatest(scroller, true);
    assert.deepEqual(scroller.focused, { preventScroll: true });
    script.unfollow(scroller);

    const unfollowed = chat();
    script.scrollToLatest(unfollowed, false);
    assert.equal(unfollowed.scrollTop, 600);
});

test('unfollowing removes the listener and the observer, and a scroller is followed once', () => {
    const scroller = chat();
    script.follow(scroller, new FakeBridge());
    script.follow(scroller, new FakeBridge());
    assert.equal(resizeObservers().length, 1);

    script.unfollow(scroller);
    script.unfollow(scroller);
    script.unfollow(null);

    assert.ok([...scroller.listeners.values()].every((handlers) => handlers.length === 0));
    assert.ok(resizeObservers()[0].disconnected);
});

test('an overflow watcher whose root left the page lets go by itself', () => {
    const root = new FakeElement();
    script.watchOverflow(root);

    root.isConnected = false;
    resizeObservers()[0].fire();

    assert.ok(FakeObserver.instances.every((o) => o.disconnected));
    script.unwatch(null);
});

// An event as the browser raises it: the script's preventDefault marks it.
function event(properties = {}) {
    const raised = {
        defaultPrevented: false,
        propagationStopped: false,
        preventDefault() {
            raised.defaultPrevented = true;
        },
        stopPropagation() {
            raised.propagationStopped = true;
        },
        ...properties,
    };
    return raised;
}

function view({ takesFiles = true } = {}) {
    const root = new FakeElement({ className: 'pch' });
    const box = root.append(new FakeElement({ className: 'pch-prompt-box' }));
    box.value = '';
    const input = takesFiles ? root.append(new FakeElement({ className: 'pch-file-input' })) : null;
    return { root, box, input };
}

// The script raises the event on the element the browser raised it on; the fake passes it to the root's handlers.
function press(root, target, properties) {
    const raised = event({ target, ...properties });
    for (const handler of root.listeners.get('keydown') ?? []) {
        handler(raised);
    }
    return raised;
}

test('Enter in the box sends what it holds, and Shift+Enter, a modifier or a key elsewhere does not', () => {
    const { root, box } = view();
    const bridge = new FakeBridge();
    script.watchPrompt(root, box, bridge);
    box.value = 'Hello';

    assert.equal(press(root, box, { key: 'Enter' }).defaultPrevented, true);
    assert.deepEqual(bridge.calls, [['Submit', 'Hello']]);

    for (const modifier of ['shiftKey', 'ctrlKey', 'altKey', 'metaKey']) {
        assert.equal(press(root, box, { key: 'Enter', [modifier]: true }).defaultPrevented, false);
    }
    assert.equal(press(root, root, { key: 'Enter' }).defaultPrevented, false);
    assert.equal(bridge.calls.length, 1);
    script.unwatchPrompt(root);
});

test('an Enter or an Esc that belongs to an input method composing text neither sends nor stops', () => {
    const { root, box } = view();
    const bridge = new FakeBridge();
    script.watchPrompt(root, box, bridge);

    assert.equal(press(root, box, { key: 'Enter', isComposing: true }).defaultPrevented, false);
    // Some browsers report the composition only by this key code.
    assert.equal(press(root, box, { key: 'Enter', keyCode: 229 }).defaultPrevented, false);
    root.dataset.generating = '';
    press(root, box, { key: 'Escape', isComposing: true });
    assert.deepEqual(bridge.calls, []);

    press(root, box, { key: 'Escape' });
    assert.deepEqual(bridge.calls, [['Escape']]);
    script.unwatchPrompt(root);
});

test('Esc stops a reply or ends an edit and goes no further, and is left to the page otherwise', () => {
    const { root, box } = view();
    const bridge = new FakeBridge();
    script.watchPrompt(root, box, bridge);

    const idle = press(root, box, { key: 'Escape' });
    assert.equal(idle.defaultPrevented, false);
    assert.equal(idle.propagationStopped, false);
    assert.deepEqual(bridge.calls, []);

    for (const mark of ['generating', 'editing']) {
        root.dataset[mark] = '';
        const handled = press(root, root, { key: 'Escape' });
        assert.equal(handled.defaultPrevented, true);
        assert.equal(handled.propagationStopped, true);
        delete root.dataset[mark];
    }
    assert.deepEqual(bridge.calls, [['Escape'], ['Escape']]);
    script.unwatchPrompt(root);
});

test('files dragged over a view that takes them are welcome, marked, and dropped into its file input', () => {
    const { root, box, input } = view();
    script.watchPrompt(root, box, new FakeBridge());
    const files = ['a.png'];

    const over = event({ dataTransfer: { types: ['Files'], dropEffect: 'none' } });
    root.dispatch('dragover', over);
    assert.equal(over.defaultPrevented, true);
    assert.equal(over.dataTransfer.dropEffect, 'copy');
    assert.equal(root.dataset.dropping, '');

    // Moving onto a child of the view is not leaving it.
    root.dispatch('dragleave', event({ relatedTarget: box }));
    assert.equal(root.dataset.dropping, '');
    root.dispatch('dragleave', event({ relatedTarget: null }));
    assert.equal('dropping' in root.dataset, false);

    const drop = event({ dataTransfer: { types: ['Files'], files } });
    root.dispatch('drop', drop);
    assert.equal(drop.defaultPrevented, true);
    assert.equal(input.files, files);
    assert.equal(input.dispatched.length, 1);
    assert.equal(input.dispatched[0].type, 'change');
    assert.equal(input.dispatched[0].bubbles, true);
    script.unwatchPrompt(root);
});

test('a view that takes no files leaves drags, drops and pasted files to the page', () => {
    const { root, box } = view({ takesFiles: false });
    script.watchPrompt(root, box, new FakeBridge());

    const over = event({ dataTransfer: { types: ['Files'] } });
    root.dispatch('dragover', over);
    const text = event({ dataTransfer: { types: ['text/plain'] } });
    root.dispatch('dragover', text);
    const drop = event({ dataTransfer: { files: ['a.png'] } });
    root.dispatch('drop', drop);
    const paste = event({ clipboardData: { files: ['a.png'] } });
    box.dispatch('paste', paste);

    assert.equal(over.defaultPrevented, false);
    assert.equal(text.defaultPrevented, false);
    assert.equal(drop.defaultPrevented, false);
    assert.equal(paste.defaultPrevented, false);
    assert.equal('dropping' in root.dataset, false);
    script.unwatchPrompt(root);
});

test('a picture pasted in the box is attached, and text pastes as text', () => {
    const { root, box, input } = view();
    script.watchPrompt(root, box, new FakeBridge());

    const text = event({ clipboardData: { files: [] } });
    box.dispatch('paste', text);
    assert.equal(text.defaultPrevented, false);
    const empty = event({ dataTransfer: { files: [] } });
    root.dispatch('drop', empty);
    assert.equal(empty.defaultPrevented, false);
    assert.equal(input.dispatched, undefined);

    // A copy from a spreadsheet carries its text and a picture of it: the text is what the user pastes.
    const table = event({ clipboardData: { files: ['image.png'], getData: (type) => (type === 'text/html' ? '<table>' : '') } });
    box.dispatch('paste', table);
    assert.equal(table.defaultPrevented, false);
    assert.equal(input.dispatched, undefined);

    const files = ['pasted.png'];
    const picture = event({ clipboardData: { files, getData: () => '' } });
    box.dispatch('paste', picture);
    assert.equal(picture.defaultPrevented, true);
    assert.equal(input.files, files);
    script.unwatchPrompt(root);
});

test('a view whose circuit went away does not fail when the user presses Enter', async () => {
    const { root, box } = view();
    const bridge = new FakeBridge({ fails: true });
    const unhandled = [];
    const record = (reason) => unhandled.push(reason);
    process.on('unhandledRejection', record);
    script.watchPrompt(root, box, bridge);

    press(root, box, { key: 'Enter' });
    await new Promise((resolve) => setImmediate(resolve));

    process.off('unhandledRejection', record);
    assert.deepEqual(unhandled, []);
    script.unwatchPrompt(root);
});

test('a box is watched once, and unwatching removes every handler', () => {
    const { root, box } = view();
    script.watchPrompt(root, box, new FakeBridge());
    script.watchPrompt(root, box, new FakeBridge());
    assert.equal(root.listeners.get('keydown').length, 1);

    script.unwatchPrompt(root);
    script.unwatchPrompt(root);
    script.unwatchPrompt(null);

    assert.ok([...root.listeners.values()].every((handlers) => handlers.length === 0));
    assert.ok([...box.listeners.values()].every((handlers) => handlers.length === 0));
});
