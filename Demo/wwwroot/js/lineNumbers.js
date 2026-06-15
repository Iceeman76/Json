window.lineNumbers = {
    register: function (elementId, gutterId) {
        const element = document.getElementById(elementId);
        const gutter = document.getElementById(gutterId);

        if (!element || !gutter || element.dataset.lineNumbersRegistered === "true") {
            return;
        }

        element.dataset.lineNumbersRegistered = "true";

        const sync = function () {
            gutter.scrollTop = element.scrollTop;
        };

        element.addEventListener("scroll", sync);
        element.addEventListener("input", sync);
    }
};
