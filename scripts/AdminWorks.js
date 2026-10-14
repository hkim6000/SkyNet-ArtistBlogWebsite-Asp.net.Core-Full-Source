var AdminWorksJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminWorks/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('aw-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('aw-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('aw-open');
        }, 3000);
    }

    function openNav() {
        el('aw-nav').classList.add('aw-open');
        el('aw-scrim').classList.add('aw-open');
    }

    function closeNav() {
        el('aw-nav').classList.remove('aw-open');
        el('aw-scrim').classList.remove('aw-open');
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('ab_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('ab_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        setCookie('ab_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('aw-modal').classList.add('aw-open');
    }

    function closeModal() {
        var m = el('aw-modal');
        if (m) {
            m.classList.remove('aw-open');
        }
    }

    var queue = [];
    var busy = false;
    var drag = null;
    var startOrder = '';

    function loaded() {
        var d = el('aw-drop');
        if (!d || d.getAttribute('data-ready')) {
            return;
        }
        d.setAttribute('data-ready', '1');
        ['dragenter', 'dragover'].forEach(function (n) {
            d.addEventListener(n, function (e) {
                e.preventDefault();
                d.classList.add('aw-over');
            });
        });
        ['dragleave', 'drop'].forEach(function (n) {
            d.addEventListener(n, function (e) {
                e.preventDefault();
                d.classList.remove('aw-over');
            });
        });
        d.addEventListener('drop', function (e) {
            add(e.dataTransfer.files);
        });
    }

    function choose() {
        el('aw-file').click();
    }

    function chosen(input) {
        add(input.files);
        input.value = '';
    }

    function note(text, bad) {
        var li = document.createElement('li');
        li.textContent = text;
        if (bad) {
            li.className = 'aw-bad';
        }
        el('aw-prog').appendChild(li);
        return li;
    }

    function add(files) {
        for (var i = 0; i < files.length; i++) {
            var f = files[i];
            if (!/^image\/(jpeg|png|webp)$/.test(f.type)) {
                note(f.name + ': only JPG, PNG or WebP', true);
                continue;
            }
            if (f.size > 30 * 1024 * 1024) {
                note(f.name + ': larger than 30 MB', true);
                continue;
            }
            queue.push({ file: f, li: note(f.name + ': waiting') });
        }
        next();
    }

    function shrink(img, max) {
        var r = Math.min(1, max / Math.max(img.width, img.height));
        var c = document.createElement('canvas');
        c.width = Math.round(img.width * r);
        c.height = Math.round(img.height * r);
        c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
        var u = c.toDataURL('image/webp', 0.86);
        if (u.indexOf('data:image/webp') !== 0) {
            u = c.toDataURL('image/jpeg', 0.88);
        }
        return { url: u, w: c.width, h: c.height };
    }

    function next() {
        if (busy || queue.length === 0) {
            return;
        }
        busy = true;
        var job = queue.shift();
        job.li.textContent = job.file.name + ': resizing';
        var img = new Image();
        var src = URL.createObjectURL(job.file);
        img.onload = function () {
            var full = shrink(img, 1600);
            var thumb = shrink(img, 720);
            URL.revokeObjectURL(src);
            job.li.textContent = job.file.name + ': uploading';
            window.__abJob = job;
            api('Upload', [
                { key: 'name', vlu: job.file.name },
                { key: 'full', vlu: full.url },
                { key: 'thumb', vlu: thumb.url },
                { key: 'w', vlu: String(full.w) },
                { key: 'h', vlu: String(full.h) }
            ]);
        };
        img.onerror = function () {
            URL.revokeObjectURL(src);
            job.li.textContent = job.file.name + ': could not be read';
            job.li.className = 'aw-bad';
            busy = false;
            next();
        };
        img.src = src;
    }

    function uploaded(ok, msg) {
        var job = window.__abJob;
        if (job) {
            job.li.textContent = job.file.name + ': ' + msg;
            job.li.className = ok ? '' : 'aw-bad';
        }
        busy = false;
        next();
    }

    function order() {
        var items = document.querySelectorAll('.aw-item');
        var ids = [];
        for (var i = 0; i < items.length; i++) {
            ids.push(items[i].getAttribute('data-id'));
        }
        return ids.join(',');
    }

    function grab(e, btn) {
        e.preventDefault();
        drag = btn.closest('.aw-item');
        startOrder = order();
        drag.classList.add('aw-dragging');
        document.addEventListener('pointermove', dragMove);
        document.addEventListener('pointerup', dragEnd);
        document.addEventListener('pointercancel', dragEnd);
    }

    function dragMove(ev) {
        if (!drag) {
            return;
        }
        ev.preventDefault();
        var t = document.elementFromPoint(ev.clientX, ev.clientY);
        var over = t ? t.closest('.aw-item') : null;
        if (!over || over === drag) {
            return;
        }
        var r = over.getBoundingClientRect();
        var after = ev.clientY > r.bottom - r.height / 3 || (ev.clientY > r.top + r.height / 3 && ev.clientX > r.left + r.width / 2);
        over.parentNode.insertBefore(drag, after ? over.nextSibling : over);
    }

    function dragEnd() {
        document.removeEventListener('pointermove', dragMove);
        document.removeEventListener('pointerup', dragEnd);
        document.removeEventListener('pointercancel', dragEnd);
        if (!drag) {
            return;
        }
        drag.classList.remove('aw-dragging');
        drag = null;
        if (order() !== startOrder) {
            saveOrder();
        }
    }

    function move(id, dir) {
        var it = document.querySelector('.aw-item[data-id="' + id + '"]');
        if (!it) {
            return;
        }
        if (dir < 0 && it.previousElementSibling) {
            it.parentNode.insertBefore(it, it.previousElementSibling);
        } else if (dir > 0 && it.nextElementSibling) {
            it.parentNode.insertBefore(it.nextElementSibling, it);
        } else {
            return;
        }
        saveOrder();
    }

    function saveOrder() {
        api('Order', [{ key: 'ids', vlu: order() }]);
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function star(id) {
        api('Feature', [{ key: 'id', vlu: id }]);
    }

    function save() {
        api('Save', [
            { key: 'id', vlu: el('aw-wid').value },
            { key: 'title', vlu: val('aw-wtitle') },
            { key: 'series', vlu: el('aw-wseries').value },
            { key: 'year', vlu: val('aw-wyear') },
            { key: 'caption', vlu: val('aw-wcaption') },
            { key: 'featured', vlu: el('aw-wfeat').checked ? '1' : '0' }
        ]);
        return false;
    }

    function remove() {
        if (confirm('Delete this photograph from the site?')) {
            api('Delete', [{ key: 'id', vlu: el('aw-wid').value }]);
        }
    }

    function addSeries() {
        var box = el('aw-ser');
        var row = document.createElement('div');
        row.className = 'aw-srow';
        row.setAttribute('data-key', '');
        row.innerHTML = '<input type="text" maxlength="40" placeholder="Series title" aria-label="Series title"><input type="text" maxlength="160" placeholder="One line about it" aria-label="Description"><small>new</small>';
        box.appendChild(row);
        row.querySelector('input').focus();
    }

    function saveSeries() {
        var rows = document.querySelectorAll('.aw-srow');
        var list = [];
        for (var i = 0; i < rows.length; i++) {
            var ins = rows[i].querySelectorAll('input');
            list.push({ key: rows[i].getAttribute('data-key'), title: ins[0].value.trim(), text: ins[1].value.trim() });
        }
        api('SaveSeries', [{ key: 'list', vlu: JSON.stringify(list) }]);
    }

    function reveal() {
        if (!getCookie('ab_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', []);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('aw-', 'aw-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        loaded: loaded,
        choose: choose,
        chosen: chosen,
        uploaded: uploaded,
        grab: grab,
        move: move,
        edit: edit,
        star: star,
        save: save,
        remove: remove,
        addSeries: addSeries,
        saveSeries: saveSeries
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminWorksJs.reveal);
} else {
    AdminWorksJs.reveal();
}
