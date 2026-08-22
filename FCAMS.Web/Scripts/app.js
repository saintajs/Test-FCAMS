// FCAMS app.js
// Minimal client-side enhancements. All validation-critical logic runs
// server-side (see the [Required]/[Range]/etc. DataAnnotations on the
// ViewModels and ModelState checks in every POST action) -- this file only
// adds convenience behavior, never anything the app depends on for
// correctness or security.
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        // Auto-dismiss success/info alerts after a few seconds.
        var alerts = document.querySelectorAll(".alert-success");
        alerts.forEach(function (el) {
            setTimeout(function () {
                el.style.transition = "opacity .4s ease";
                el.style.opacity = "0";
                setTimeout(function () { el.remove(); }, 400);
            }, 5000);
        });

        // Confirm before submitting any form flagged data-confirm="...".
        document.querySelectorAll("form[data-confirm]").forEach(function (form) {
            form.addEventListener("submit", function (e) {
                if (!window.confirm(form.getAttribute("data-confirm"))) {
                    e.preventDefault();
                }
            });
        });
    });
})();
