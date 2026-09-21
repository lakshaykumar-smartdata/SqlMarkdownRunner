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
