// Persists the chosen colour theme ("dark" | "light") in the browser.
window.l2pTheme = {
    get: function () {
        try {
            return window.localStorage.getItem("l2p-theme");
        } catch (e) {
            return null;
        }
    },
    set: function (value) {
        try {
            window.localStorage.setItem("l2p-theme", value);
        } catch (e) {
            // Storage can be blocked (private mode); the theme then just is not remembered.
        }
    }
};
