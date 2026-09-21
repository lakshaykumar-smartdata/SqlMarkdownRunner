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
