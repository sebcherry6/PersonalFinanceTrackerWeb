// FinanceTracker by WebCherry - the only script on the site.
//
// The Content-Security-Policy blocks inline scripts and onclick="" attributes, so behaviour
// is attached here instead. The site works fully without JavaScript; this only adds polish.

(function () {
    "use strict";

    // 1. Confirm before destructive actions.
    //    Any <form data-confirm="Question?"> asks first and cancels if the user says no.
    document.addEventListener("submit", function (event) {
        var form = event.target;
        var question = form.getAttribute("data-confirm");
        if (question && !window.confirm(question)) {
            event.preventDefault();
        }
    }, true);

    // 2. Prevent double submissions (e.g. adding the same transaction twice).
    //    The button is disabled on the *next* tick: disabling it immediately would drop its
    //    name/value from the submitted data - which would break the Goals "Withdraw" button.
    document.addEventListener("submit", function (event) {
        if (event.defaultPrevented) return;
        var buttons = event.target.querySelectorAll("button[type=submit], button:not([type])");
        window.setTimeout(function () {
            buttons.forEach(function (b) { b.disabled = true; });
        }, 0);
    });

    // 3. Re-enable buttons if the page is restored from the back/forward cache.
    window.addEventListener("pageshow", function (event) {
        if (event.persisted) {
            document.querySelectorAll("button:disabled").forEach(function (b) { b.disabled = false; });
        }
    });

    // 4. Category list follows "Money out / Money in" on the transaction form, so you only
    //    see categories that are valid for the chosen type. (The server checks this too.)
    var form = document.querySelector("[data-transaction-form]");
    if (form) {
        var select = form.querySelector("select[name=Category]");
        var radios = form.querySelectorAll("input[name=Type]");

        var sync = function () {
            var checked = form.querySelector("input[name=Type]:checked");
            if (!checked || !select) return;
            var showGroup = checked.value === "Income" ? "income" : "expense";

            select.querySelectorAll("optgroup").forEach(function (group) {
                var visible = group.getAttribute("data-group") === showGroup || group.getAttribute("data-group") === "both";
                group.hidden = !visible;
                group.disabled = !visible;
            });

            var selected = select.options[select.selectedIndex];
            if (!selected || selected.parentElement.disabled) {
                var first = select.querySelector("optgroup:not([disabled]) option");
                if (first) select.value = first.value;
            }
        };

        radios.forEach(function (r) { r.addEventListener("change", sync); });
        sync();
    }
})();
