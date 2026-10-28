// Smoothly scrolls the element with the given id to the top of the viewport.
window.l2pScroll = {
    intoView: function (id) {
        var element = document.getElementById(id);
        if (element) {
            element.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    }
};
