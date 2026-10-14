var AdminLayoutJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminLayout/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ay-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ay-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ay-open');
        }, 3000);
    }

    function openNav() {
        el('ay-nav').classList.add('ay-open');
        el('ay-scrim').classList.add('ay-open');
    }

    function closeNav() {
        el('ay-nav').classList.remove('ay-open');
        el('ay-scrim').classList.remove('ay-open');
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
        el('ay-modal').classList.add('ay-open');
    }

    function closeModal() {
        var m = el('ay-modal');
        if (m) {
            m.classList.remove('ay-open');
        }
    }

    var page = 'home';
    var dirty = false;
    var drag = null;
    var startOrder = '';
    var target = null;

    function loaded() {
        dirty = false;
        mark();
        var c = el('ay-canvas');
        if (!c) {
            return;
        }
        c.addEventListener('input', function () {
            dirty = true;
            mark();
        });
        c.addEventListener('paste', function (e) {
            if (e.target.closest && e.target.closest('.ay-ce')) {
                e.preventDefault();
                document.execCommand('insertText', false, (e.clipboardData || window.clipboardData).getData('text/plain'));
            }
        });
        page = c.getAttribute('data-page');
        var ws = c.querySelectorAll('select[data-f=source]');
        for (var i = 0; i < ws.length; i++) {
            strip(ws[i]);
        }
        dirty = false;
        mark();
    }

    function strip(sel) {
        var blk = sel.closest('.ay-blk');
        var src = blk.querySelector('select[data-f=source]').value;
        var imgs = Array.prototype.slice.call(blk.querySelectorAll('.ay-strip img'));
        var list = imgs;
        if (src === 'featured') {
            var f = imgs.filter(function (i) { return i.getAttribute('data-ft') === '1'; });
            list = f.length ? f : imgs;
        } else if (src === 'newest') {
            list = imgs.slice().sort(function (a, b) { return (b.getAttribute('data-y') - a.getAttribute('data-y')) || (a.getAttribute('data-i') - b.getAttribute('data-i')); });
        } else {
            list = imgs.filter(function (i) { return 'series:' + i.getAttribute('data-s') === src; });
        }
        var show = list.slice(0, 6);
        imgs.forEach(function (i) { i.style.display = 'none'; });
        show.forEach(function (i) { i.style.display = ''; i.parentNode.appendChild(i); });
        touch();
    }

    function mark() {
        var d = el('ay-dirty');
        if (d) {
            d.textContent = dirty ? 'Unsaved changes' : '';
        }
    }

    function touch() {
        dirty = true;
        mark();
    }

    function tab(p) {
        if (dirty && !confirm('Leave without saving your changes?')) {
            return;
        }
        api('Show', [{ key: 'pg', vlu: p }]);
    }

    function blocks() {
        return el('ay-canvas').querySelectorAll('.ay-blk');
    }

    function order() {
        var b = blocks();
        var ids = [];
        for (var i = 0; i < b.length; i++) {
            ids.push(b[i].getAttribute('data-id'));
        }
        return ids.join(',');
    }

    function grab(e, btn) {
        e.preventDefault();
        drag = btn.closest('.ay-blk');
        startOrder = order();
        drag.classList.add('ay-dragging');
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
        var over = t ? t.closest('.ay-blk') : null;
        if (over && over !== drag) {
            var r = over.getBoundingClientRect();
            over.parentNode.insertBefore(drag, ev.clientY > r.top + r.height / 2 ? over.nextSibling : over);
        }
        if (ev.clientY < 90) {
            window.scrollBy(0, -14);
        } else if (ev.clientY > window.innerHeight - 50) {
            window.scrollBy(0, 14);
        }
    }

    function dragEnd() {
        document.removeEventListener('pointermove', dragMove);
        document.removeEventListener('pointerup', dragEnd);
        document.removeEventListener('pointercancel', dragEnd);
        if (!drag) {
            return;
        }
        drag.classList.remove('ay-dragging');
        drag = null;
        if (order() !== startOrder) {
            touch();
        }
    }

    function move(btn, dir) {
        var b = btn.closest('.ay-blk');
        if (dir < 0 && b.previousElementSibling) {
            b.parentNode.insertBefore(b, b.previousElementSibling);
        } else if (dir > 0 && b.nextElementSibling) {
            b.parentNode.insertBefore(b.nextElementSibling, b);
        } else {
            return;
        }
        b.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        touch();
    }

    function drop(btn) {
        var b = btn.closest('.ay-blk');
        if (confirm('Remove this block from the page?')) {
            b.parentNode.removeChild(b);
            touch();
        }
    }

    function flip(btn) {
        var b = btn.closest('.ay-blk');
        var on = b.getAttribute('data-flip') !== '1';
        b.setAttribute('data-flip', on ? '1' : '0');
        b.querySelector('.ay-vsplit').classList.toggle('ay-flip', on);
        touch();
    }

    function photo(btn) {
        target = btn.closest('.ay-blk');
        el('ay-mbody').innerHTML = el('ay-picks').innerHTML;
        openModal();
    }

    function pick(src) {
        closeModal();
        if (!target) {
            return;
        }
        target.setAttribute('data-image', src);
        var img = target.querySelector('img[data-main]');
        if (img) {
            img.src = src;
        }
        touch();
    }

    function add(type) {
        api('Add', [{ key: 'type', vlu: type }]);
    }

    function added() {
        var h = el('ay-holder');
        var b = h.firstElementChild;
        if (b) {
            el('ay-canvas').appendChild(b);
            b.scrollIntoView({ block: 'center', behavior: 'smooth' });
            var f = b.querySelector('.ay-ce');
            if (f) {
                f.focus();
                var r = document.createRange();
                r.selectNodeContents(f);
                var sel = window.getSelection();
                sel.removeAllRanges();
                sel.addRange(r);
            }
            touch();
        }
    }

    function collect() {
        var b = blocks();
        var out = [];
        for (var i = 0; i < b.length; i++) {
            var o = { id: b[i].getAttribute('data-id'), type: b[i].getAttribute('data-type'), title: '', text: '', image: b[i].getAttribute('data-image') || '', flip: b[i].getAttribute('data-flip') === '1' };
            var fs = b[i].querySelectorAll('[data-f]');
            for (var j = 0; j < fs.length; j++) {
                var f = fs[j];
                o[f.getAttribute('data-f')] = f.tagName === 'SELECT' ? f.value : f.innerText.replace(/ /g, ' ').replace(/\n{3,}/g, '\n\n').trim();
            }
            out.push(o);
        }
        return out;
    }

    function save() {
        api('Save', [{ key: 'pg', vlu: page }, { key: 'blocks', vlu: JSON.stringify(collect()) }]);
    }

    function saved() {
        dirty = false;
        mark();
        toast('Page saved');
    }

    function undo() {
        if (confirm('Restore the whole site to the version before your last save?')) {
            api('Undo', [{ key: 'pg', vlu: page }]);
        }
    }

    window.addEventListener('beforeunload', function (e) {
        if (dirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

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
            var id = e.target && e.target.id ? e.target.id.replace('ay-', 'ay-e-') : '';
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
        strip: strip,
        touch: touch,
        tab: tab,
        grab: grab,
        move: move,
        drop: drop,
        flip: flip,
        photo: photo,
        pick: pick,
        add: add,
        added: added,
        save: save,
        saved: saved,
        undo: undo
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLayoutJs.reveal);
} else {
    AdminLayoutJs.reveal();
}
