// Persists the chosen colour theme ("dark" | "light") in the browser and mirrors it to the data-theme attribute.
// l2pTheme.apply (read the stored theme, dark by default, set data-theme) is defined by the inline script in the <head> of
// App.razor, because it has to run before first paint; this file extends that object.
(function () {
    var theme = window.l2pTheme = window.l2pTheme || {};

    // The theme in effect. data-theme is normally set by apply(); if it is missing (e.g. dropped by an enhanced
    // navigation before the enhancedload handler ran), it is re-applied from storage first.
    theme.current = function () {
        var value = document.documentElement.dataset.theme;
        if (value !== "dark" && value !== "light") {
            value = theme.apply();
        }
        return value;
    };

    theme.set = function (value) {
        document.documentElement.dataset.theme = value;
        try {
            window.localStorage.setItem("l2p-theme", value);
        } catch (e) {
            // Storage can be blocked (private mode); the theme then just is not remembered.
        }
    };

    // Enhanced navigation (links and forms between pages) replaces the document from fetched HTML: it syncs the
    // <html> attributes, which removes data-theme, and does not run the inline <head> script again. The event is
    // dispatched synchronously after each DOM update, so the attribute is back before the next paint.
    var registered = false;
    function register() {
        if (registered || !window.Blazor || typeof window.Blazor.addEventListener !== "function") {
            return;
        }
        registered = true;
        window.Blazor.addEventListener("enhancedload", function () {
            theme.apply();
        });
    }

    // blazor.web.js is loaded before this file and starts (creating addEventListener) synchronously, so this
    // normally registers right away; the DOMContentLoaded retry covers a different script order.
    register();
    if (!registered) {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", register, { once: true });
        } else {
            window.setTimeout(register, 0);
        }
    }
})();
