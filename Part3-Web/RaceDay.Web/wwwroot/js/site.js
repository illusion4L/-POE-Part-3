// Confirm before destructive actions: <form data-confirm="Delete this event?">
document.addEventListener('submit', function (e) {
    var message = e.target && e.target.dataset ? e.target.dataset.confirm : null;
    if (message && !window.confirm(message)) e.preventDefault();
});

// Live preview of a chosen banner image: <input type="file" data-preview="#target-img">
document.addEventListener('change', function (e) {
    var input = e.target;
    if (!input.matches || !input.matches('input[type=file][data-preview]')) return;
    var target = document.querySelector(input.dataset.preview);
    if (!target) return;
    var file = input.files && input.files[0];
    if (!file) { target.classList.add('d-none'); return; }
    if (target.dataset.url) URL.revokeObjectURL(target.dataset.url);
    target.dataset.url = URL.createObjectURL(file);
    target.src = target.dataset.url;
    target.classList.remove('d-none');
});

// Don't double-submit forms
document.addEventListener('submit', function (e) {
    var form = e.target;
    if (e.defaultPrevented || !form.matches || form.dataset.confirm) return;
    window.setTimeout(function () {
        if (e.defaultPrevented) return;
        form.querySelectorAll('button[type=submit]:not([data-keep])').forEach(function (b) {
            b.disabled = true;
            b.dataset.locked = '1';
        });
    }, 0);
});

// Coming back with the Back button should not leave buttons greyed out
window.addEventListener('pageshow', function (e) {
    if (!e.persisted) return;
    document.querySelectorAll('button[data-locked]').forEach(function (b) { b.disabled = false; delete b.dataset.locked; });
});
