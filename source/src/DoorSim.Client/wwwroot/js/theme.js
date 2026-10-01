// Theme mode persistence. The inline script at the top of index.html resolves the stored mode
// before first paint (so the boot loader is themed); this file is what Blazor calls afterwards.
// Both share the "doorsim.theme" key and the data-theme attribute on <html>, which custom CSS
// keys off via html[data-theme="dark"].
window.doorsimTheme = {
    key: "doorsim.theme",

    getMode: function () {
        try {
            var mode = localStorage.getItem(this.key);
            return mode === "light" || mode === "dark" ? mode : "system";
        } catch (e) {
            return "system";
        }
    },

    setMode: function (mode) {
        try {
            localStorage.setItem(this.key, mode);
        } catch (e) {
            // Storage blocked: the choice just won't survive a reload.
        }
    },

    isDark: function () {
        return document.documentElement.dataset.theme === "dark";
    },

    apply: function (isDark) {
        document.documentElement.dataset.theme = isDark ? "dark" : "light";
    }
};
