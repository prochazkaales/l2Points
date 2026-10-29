// Persists the chosen colour theme ("dark" | "light") in the browser and mirrors it to the data-theme attribute.
window.l2pTheme = {
    // The theme in effect: data-theme is set before first paint by the inline script in App.razor.
    current: function () {
        return document.documentElement.dataset.theme === "light" ? "light" : "dark";
    },
    get: function () {
        try {
            return window.localStorage.getItem("l2p-theme");
        } catch (e) {
            return null;
        }
    },
    set: function (value) {
        document.documentElement.dataset.theme = value;
        try {
            window.localStorage.setItem("l2p-theme", value);
        } catch (e) {
            // Storage can be blocked (private mode); the theme then just is not remembered.
        }
    }
};
