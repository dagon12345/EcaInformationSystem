window.viewerKey = {
    get: function () {
        let key = localStorage.getItem('eca_viewer_key');
        if (!key) {
            key = crypto.randomUUID();
            localStorage.setItem('eca_viewer_key', key);
        }
        return key;
    }
};