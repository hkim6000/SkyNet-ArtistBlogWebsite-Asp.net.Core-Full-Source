var AdminCommentsJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminComments/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ac-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ac-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ac-open');
        }, 3000);
    }

    function openNav() {
        el('ac-nav').classList.add('ac-open');
        el('ac-scrim').classList.add('ac-open');
    }

    function closeNav() {
        el('ac-nav').classList.remove('ac-open');
        el('ac-scrim').classList.remove('ac-open');
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
        el('ac-modal').classList.add('ac-open');
    }

    function closeModal() {
        var m = el('ac-modal');
        if (m) {
            m.classList.remove('ac-open');
        }
    }

    var tab = 'pending';

    function loaded() {
    }

    function show(t) {
        tab = t;
        api('Show', [{ key: 'tab', vlu: t }]);
    }

    function act(id, a) {
        if (a === 'delete' && !confirm('Delete this comment for good?')) {
            return;
        }
        api('Act', [{ key: 'id', vlu: id }, { key: 'act', vlu: a }, { key: 'tab', vlu: tab }]);
    }

    function all() {
        if (confirm('Approve every waiting comment?')) {
            api('Act', [{ key: 'id', vlu: '*' }, { key: 'act', vlu: 'approve' }, { key: 'tab', vlu: tab }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('ac-', 'ac-e-') : '';
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
        act: act,
        all: all
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminCommentsJs.reveal);
} else {
    AdminCommentsJs.reveal();
}
