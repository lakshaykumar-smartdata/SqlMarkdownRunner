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

// Pasting a second query after a first one needs a GO between them, so put it there.
document.addEventListener('paste', e => {
    const editor = e.target;
    if (!editor.matches?.('.sql-editor textarea')) return;

    const pasted = e.clipboardData?.getData('text') ?? '';
    const before = editor.value.slice(0, editor.selectionStart);
    const after = editor.value.slice(editor.selectionEnd);

    if (!pasted.trim() || !before.trim()) return;          // nothing to separate
    if (/(^|\n)[ \t]*GO[ \t]*\r?\n?[ \t]*$/i.test(before)) return;   // already separated
    if (/^\s*GO\b/i.test(pasted)) return;                  // the paste brings its own

    e.preventDefault();
    const head = before.replace(/\s*$/, '') + '\nGO\n' + pasted;
    editor.value = head + after;
    editor.selectionStart = editor.selectionEnd = head.length;
    editor.dispatchEvent(new Event('change', { bubbles: true }));
});
