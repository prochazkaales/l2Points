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

// Marks a horizontally scrolled table container with l2p-scrolled (scrollLeft > 0), so the pinned column casts its
// shadow only once the content has actually moved under it. One capturing listener covers every current and future
// container: scroll events do not bubble, but they can be captured on the document.
(function () {
    var containers = ".l2p-grid-scroll, .l2p-wk-grid-scroll, .mud-table-container";
    document.addEventListener("scroll", function (event) {
        var target = event.target;
        if (target && target.nodeType === 1 && target.matches(containers)) {
            target.classList.toggle("l2p-scrolled", target.scrollLeft > 0);
        }
    }, { capture: true, passive: true });
})();
