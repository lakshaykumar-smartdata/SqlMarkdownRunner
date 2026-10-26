window.smr = {
    copy: text => navigator.clipboard.writeText(text),
    download: (name, text) => {
        const a = document.createElement('a');
        a.href = URL.createObjectURL(new Blob([text], { type: 'text/markdown' }));
        a.download = name;
        a.click();
        URL.revokeObjectURL(a.href);
    }
};

// Names in the object panel drag into the editor; textareas accept a text drop natively.
document.addEventListener('dragstart', e => {
    const item = e.target.closest?.('[data-drag]');
    if (item) e.dataTransfer.setData('text/plain', item.dataset.drag);
});

// A second query after a first one needs a GO between them.
const needsGo = (before, pasted) =>
    !!pasted.trim() && !!before.trim()
    && !/(^|\n)[ \t]*GO[ \t]*\r?\n?[ \t]*$/i.test(before)   // already separated
    && !/^\s*GO\b/i.test(pasted);                           // the paste brings its own

const insert = (editor, text) => {
    const before = editor.value.slice(0, editor.selectionStart);
    const after = editor.value.slice(editor.selectionEnd);
    const head = (needsGo(before, text) ? before.replace(/\s*$/, '') + '\nGO\n' : before) + text;

    editor.value = head + after;
    editor.selectionStart = editor.selectionEnd = head.length;
    editor.dispatchEvent(new Event('change', { bubbles: true }));
};

document.addEventListener('paste', e => {
    const editor = e.target;
    if (!editor.matches?.('.sql-editor textarea')) return;

    // Leave the browser's own paste alone unless a GO has to go in — it keeps the undo history.
    if (!needsGo(editor.value.slice(0, editor.selectionStart), e.clipboardData?.getData('text') ?? '')) return;

    e.preventDefault();
    insert(editor, e.clipboardData.getData('text'));
});

// The Paste button: same insert, but the text comes from the clipboard API rather than an event.
window.smr.paste = async () => {
    const editor = document.querySelector('.sql-editor textarea');
    const text = editor ? await navigator.clipboard.readText() : '';
    if (!text) return false;

    // Clicking the button moved focus off the editor, so an untouched editor reads caret 0 —
    // that would prepend. Put the text at the end unless the caret is genuinely in there.
    if (document.activeElement !== editor) editor.selectionStart = editor.selectionEnd = editor.value.length;
    editor.focus();
    insert(editor, text);
    return true;
};

// Blazor hands binary back as base64; turn it into a file the browser will save.
window.smr.downloadBytes = (name, base64, type) => {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([bytes], { type }));
    a.download = name;
    a.click();
    URL.revokeObjectURL(a.href);
};
