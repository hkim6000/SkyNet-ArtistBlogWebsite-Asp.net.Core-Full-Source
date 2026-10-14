var HomeJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Home/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('hm-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('hm-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('hm-open');
        }, 3000);
    }

    function openNav() {
        el('hm-nav').classList.add('hm-open');
        el('hm-scrim').classList.add('hm-open');
    }

    function closeNav() {
        el('hm-nav').classList.remove('hm-open');
        el('hm-scrim').classList.remove('hm-open');
    }

    var ARG = '';

    function argValue() {
        return ARG ? (new URLSearchParams(location.search).get(ARG) || '') : '';
    }

    function applyTheme(theme) {
        var site = el('hm-site');
        if (site) {
            site.setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
        }
    }

    function toggleTheme() {
        var site = el('hm-site');
        var next = site.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('ab-theme', next);
        } catch (e) {
        }
    }

    function hydrated(ok, accent, theme) {
        document.documentElement.classList.remove('hm-hydrating');
        if (!ok) {
            setCookie('ab_sbx', '', 0);
            return;
        }
        var site = el('hm-site');
        if (accent) {
            site.style.setProperty('--hm-accent', accent);
        }
        var stored = '';
        try {
            stored = localStorage.getItem('ab-theme') || '';
        } catch (e) {
        }
        applyTheme(stored || theme);
        el('hm-demo-bar').innerHTML = 'You are viewing your own demo copy. Changes reset after an hour. <a href="Admin">Open admin</a>';
        afterRender();
    }

    function sandboxed(id) {
        setCookie('ab_sbx', id, 3600);
        el('hm-demo-bar').innerHTML = 'You are viewing your own demo copy. Changes reset after an hour. <a href="Admin">Open admin</a>';
    }

    function afterRender() {
        var slug = el('hm-like');
        if (slug) {
            var k = 'ab-like-' + slug.getAttribute('data-slug');
            var on = false;
            try {
                on = localStorage.getItem(k) === '1';
            } catch (e) {
            }
            setLike(on);
        }
    }

    function setLike(on) {
        var b = el('hm-like');
        var n = parseInt(b.getAttribute('data-base'), 10) + (on ? 1 : 0);
        b.classList.toggle('hm-act', on);
        el('hm-likes-n').textContent = n;
    }

    function like() {
        var b = el('hm-like');
        var k = 'ab-like-' + b.getAttribute('data-slug');
        var on = !b.classList.contains('hm-act');
        try {
            localStorage.setItem(k, on ? '1' : '0');
        } catch (e) {
        }
        setLike(on);
    }

    function series(key) {
        call('Filter', [{ key: 'series', vlu: key }, { key: 'sbx', vlu: getCookie('ab_sbx') }]);
    }

    function filtered(key) {
        history.replaceState(null, '', 'Works' + (key ? '?s=' + encodeURIComponent(key) : ''));
    }

    function view(id) {
        var a = el('hm-act-series');
        call('Show', [{ key: 'id', vlu: id }, { key: 'series', vlu: a ? a.value : '' }, { key: 'sbx', vlu: getCookie('ab_sbx') }]);
    }

    function opened() {
        el('hm-viewer').classList.add('hm-open');
        document.body.style.overflow = 'hidden';
    }

    function closeView() {
        var v = el('hm-viewer');
        if (v) {
            v.classList.remove('hm-open');
        }
        document.body.style.overflow = '';
    }

    function tag(t) {
        el('hm-tagv').value = t;
        journal(0);
    }

    function journal(offset) {
        call('Filter', [
            { key: 'tag', vlu: val('hm-tagv') },
            { key: 'term', vlu: val('hm-q') },
            { key: 'offset', vlu: String(offset || 0) },
            { key: 'sbx', vlu: getCookie('ab_sbx') }
        ]);
    }

    var stimer = null;

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            journal(0);
        }, 300);
    }

    function tagged(t) {
        history.replaceState(null, '', 'Journal' + (t ? '?t=' + encodeURIComponent(t) : ''));
    }

    function comment() {
        call('Comment', [
            { key: 'slug', vlu: el('hm-like') ? el('hm-like').getAttribute('data-slug') : '' },
            { key: 'name', vlu: val('hm-cname') },
            { key: 'text', vlu: val('hm-ctext') },
            { key: 'sbx', vlu: getCookie('ab_sbx') }
        ]);
        return false;
    }

    function commented() {
        el('hm-ctext').value = '';
        toast('Comment sent for review');
    }

    function send() {
        call('Send', [
            { key: 'name', vlu: val('hm-name') },
            { key: 'email', vlu: val('hm-email') },
            { key: 'topic', vlu: val('hm-topic') },
            { key: 'message', vlu: val('hm-msg') },
            { key: 'sbx', vlu: getCookie('ab_sbx') }
        ]);
        return false;
    }

    function demoLink() {
        toast('Demo link \u2013 social accounts are placeholders');
        return false;
    }

    function sent() {
        el('hm-msg').value = '';
        el('hm-topic').value = '';
        toast('Message sent');
    }

    function reveal() {
        var stored = '';
        try {
            stored = localStorage.getItem('ab-theme') || '';
        } catch (e) {
        }
        if (stored) {
            applyTheme(stored);
        }
        afterRender();
        if (getCookie('ab_sbx')) {
            call('Hydrate', [{ key: 'sbx', vlu: getCookie('ab_sbx') }, { key: 'arg', vlu: argValue() }]);
        }
        document.addEventListener('keydown', function (e) {
            var v = el('hm-viewer');
            if (e.key === 'Escape') {
                closeNav();
                closeView();
            }
            if (v && v.classList.contains('hm-open')) {
                if (e.key === 'ArrowLeft' && el('hm-vprev')) {
                    el('hm-vprev').click();
                }
                if (e.key === 'ArrowRight' && el('hm-vnext')) {
                    el('hm-vnext').click();
                }
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('hm-', 'hm-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    if (document.cookie.indexOf('ab_sbx=') >= 0) {
        document.documentElement.classList.add('hm-hydrating');
        setTimeout(function () {
            document.documentElement.classList.remove('hm-hydrating');
        }, 2500);
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        hydrated: hydrated,
        sandboxed: sandboxed,
        like: like,
        series: series,
        filtered: filtered,
        view: view,
        opened: opened,
        closeView: closeView,
        tag: tag,
        journal: journal,
        search: search,
        tagged: tagged,
        comment: comment,
        commented: commented,
        send: send,
        sent: sent,
        demoLink: demoLink
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', HomeJs.reveal);
} else {
    HomeJs.reveal();
}
