// Show/hide toggle of the password fields on the static account pages (no Blazor interactivity there).
document.addEventListener("click", function (event) {
    var button = event.target.closest("button[data-l2p-password-toggle]");
    if (!button) {
        return;
    }

    var input = document.getElementById(button.getAttribute("data-l2p-password-toggle"));
    if (!input) {
        return;
    }

    var show = input.type === "password";
    input.type = show ? "text" : "password";
    button.setAttribute("aria-pressed", show ? "true" : "false");
    button.setAttribute("aria-label", show ? "Skrýt heslo" : "Zobrazit heslo");
});
