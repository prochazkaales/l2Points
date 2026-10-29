// Smoothly scrolls the element with the given id to the top of the viewport.
window.l2pScroll = {
    intoView: function (id) {
        var element = document.getElementById(id);
        if (element) {
            element.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    },

    // On phones scrolls every attendance grid so that today's column sits right after the pinned boss column
    // (all the way left when the week has no today). No animation.
    gridsToToday: function () {
        if (window.innerWidth >= 600) {
            return;
        }

        document.querySelectorAll(".l2p-grid-scroll").forEach(function (scroller) {
            var today = scroller.querySelector("th.l2p-grid-today");
            if (!today) {
                scroller.scrollLeft = 0;
                return;
            }

            scroller.scrollLeft = today.offsetLeft - today.parentElement.firstElementChild.offsetWidth;
        });
    }
};
