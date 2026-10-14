var AdminPostsJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminPosts/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ap-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ap-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ap-open');
        }, 3000);
    }

    function openNav() {
        el('ap-nav').classList.add('ap-open');
        el('ap-scrim').classList.add('ap-open');
    }

    function closeNav() {
        el('ap-nav').classList.remove('ap-open');
        el('ap-scrim').classList.remove('ap-open');
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
        el('ap-modal').classList.add('ap-open');
    }

    function closeModal() {
        var m = el('ap-modal');
        if (m) {
            m.classList.remove('ap-open');
        }
    }

    var mode = 'cover';
    var range = null;
    var filter = 'all';

    function loaded() {
        var b = el('ap-body');
        if (b) {
            try {
                document.execCommand('defaultParagraphSeparator', false, 'p');
            } catch (e) {
            }
            b.addEventListener('paste', function (e) {
                e.preventDefault();
                var t = (e.clipboardData || window.clipboardData).getData('text/plain');
                document.execCommand('insertText', false, t);
            });
        }
        list();
    }

    function list() {
        var rows = document.querySelectorAll('.ap-pl .ap-row');
        var q = val('ap-find').toLowerCase();
        for (var i = 0; i < rows.length; i++) {
            var r = rows[i];
            var ok = (filter === 'all' || r.getAttribute('data-st') === filter) && (!q || r.getAttribute('data-t').indexOf(q) >= 0);
            r.style.display = ok ? '' : 'none';
        }
    }

    function show(f) {
        filter = f;
        var cs = document.querySelectorAll('.ap-chip');
        for (var i = 0; i < cs.length; i++) {
            cs[i].classList.toggle('ap-act', cs[i].getAttribute('data-f') === f);
        }
        list();
    }

    function edit(slug) {
        api('Edit', [{ key: 'slug', vlu: slug }]);
    }

    function back() {
        api('Load', []);
        window.scrollTo(0, 0);
    }

    function cmd(c, v) {
        el('ap-body').focus();
        if (c === 'link') {
            var u = prompt('Link address (https://...)', 'https://');
            if (u) {
                document.execCommand('createLink', false, u);
            }
            return;
        }
        if (c === 'clear') {
            document.execCommand('removeFormat', false, null);
            document.execCommand('formatBlock', false, '<p>');
            return;
        }
        document.execCommand(c, false, v || null);
    }

    function picker(m) {
        mode = m;
        var s = window.getSelection();
        range = null;
        if (m === 'body' && s.rangeCount > 0 && el('ap-body').contains(s.anchorNode)) {
            range = s.getRangeAt(0).cloneRange();
        }
        openModal();
    }

    function esc(t) {
        return String(t).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
    }

    function pick(src, alt) {
        closeModal();
        if (mode === 'cover') {
            el('ap-cover').value = src;
            el('ap-cov').innerHTML = '<img src="' + esc(src) + '" alt="">';
            el('ap-e-cover').textContent = '';
            return;
        }
        var b = el('ap-body');
        b.focus();
        var s = window.getSelection();
        if (range) {
            s.removeAllRanges();
            s.addRange(range);
        } else {
            s.selectAllChildren(b);
            s.collapseToEnd();
        }
        document.execCommand('insertHTML', false, '<figure><img src="' + esc(src) + '" alt="' + esc(alt) + '"><figcaption>' + (esc(alt) || 'Caption') + '</figcaption></figure><p><br></p>');
    }

    function slugify() {
        if (el('ap-orig').value === '' && !el('ap-slug').getAttribute('data-touched')) {
            el('ap-slug').value = val('ap-title').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').substring(0, 80);
        }
    }

    function save() {
        api('Save', [
            { key: 'orig', vlu: el('ap-orig').value },
            { key: 'title', vlu: val('ap-title') },
            { key: 'slug', vlu: val('ap-slug') },
            { key: 'date', vlu: val('ap-date') },
            { key: 'tags', vlu: val('ap-tags') },
            { key: 'excerpt', vlu: val('ap-excerpt') },
            { key: 'cover', vlu: el('ap-cover').value },
            { key: 'body', vlu: el('ap-body').innerHTML },
            { key: 'published', vlu: el('ap-pub').checked ? '1' : '0' }
        ]);
    }

    function saved(slug, live) {
        el('ap-orig').value = slug;
        el('ap-slug').value = slug;
        var v = el('ap-viewlink');
        v.href = 'Post?p=' + encodeURIComponent(slug);
        v.style.display = live ? '' : 'none';
        toast(live ? 'Post saved and published' : 'Draft saved');
    }

    function remove(slug, title) {
        if (confirm('Delete "' + title + '" and its comments?')) {
            api('Delete', [{ key: 'slug', vlu: slug }]);
        }
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
            var id = e.target && e.target.id ? e.target.id.replace('ap-', 'ap-e-') : '';
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
        list: list,
        show: show,
        edit: edit,
        back: back,
        cmd: cmd,
        picker: picker,
        pick: pick,
        slugify: slugify,
        save: save,
        saved: saved,
        remove: remove
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminPostsJs.reveal);
} else {
    AdminPostsJs.reveal();
}
