var AdminMessagesJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminMessages/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('am-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('am-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('am-open');
        }, 3000);
    }

    function openNav() {
        el('am-nav').classList.add('am-open');
        el('am-scrim').classList.add('am-open');
    }

    function closeNav() {
        el('am-nav').classList.remove('am-open');
        el('am-scrim').classList.remove('am-open');
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
        el('am-modal').classList.add('am-open');
    }

    function closeModal() {
        var m = el('am-modal');
        if (m) {
            m.classList.remove('am-open');
        }
    }

    var filter = 'open';

    function loaded() {
    }

    function show(f, id) {
        filter = f;
        api('Show', [{ key: 'filter', vlu: f }, { key: 'id', vlu: id || '' }]);
    }

    function open(id) {
        api('Show', [{ key: 'filter', vlu: filter }, { key: 'id', vlu: id }]);
    }

    function opened() {
        var r = el('am-reader');
        if (r && window.innerWidth <= 1000) {
            r.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    function act(id, a) {
        if (a === 'delete' && !confirm('Delete this message for good?')) {
            return;
        }
        api('Act', [{ key: 'id', vlu: id }, { key: 'act', vlu: a }, { key: 'filter', vlu: filter }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('am-', 'am-e-') : '';
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
        show: show,
        open: open,
        opened: opened,
        act: act
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminMessagesJs.reveal);
} else {
    AdminMessagesJs.reveal();
}
