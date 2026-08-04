/**
 * Viewport-aware floating tooltips for .has-tip[data-tip].
 * Uses a single fixed-position host so tips are not clipped by overflow
 * parents and stay fully visible near screen edges.
 */
(function () {
  'use strict';

  var GAP = 8;
  var MARGIN = 8;
  var MAX_WIDTH = 288; /* 18rem @ 16px */
  var SHOW_DELAY = 80;
  var HIDE_DELAY = 40;

  var tipEl = null;
  var activeTarget = null;
  var showTimer = null;
  var hideTimer = null;
  var bound = false;

  function ensureTip() {
    if (tipEl && tipEl.isConnected) return tipEl;
    tipEl = document.createElement('div');
    tipEl.id = 'floating-tip';
    tipEl.className = 'floating-tip';
    tipEl.setAttribute('role', 'tooltip');
    tipEl.setAttribute('aria-hidden', 'true');
    document.body.appendChild(tipEl);
    return tipEl;
  }

  function clearTimers() {
    if (showTimer != null) {
      clearTimeout(showTimer);
      showTimer = null;
    }
    if (hideTimer != null) {
      clearTimeout(hideTimer);
      hideTimer = null;
    }
  }

  function hide() {
    clearTimers();
    activeTarget = null;
    if (!tipEl) return;
    tipEl.classList.remove('is-visible');
    tipEl.setAttribute('aria-hidden', 'true');
    tipEl.textContent = '';
  }

  function place(target) {
    var el = ensureTip();
    var text = (target.getAttribute('data-tip') || '').trim();
    if (!text) {
      hide();
      return;
    }

    el.textContent = text;
    el.classList.add('is-visible');
    el.setAttribute('aria-hidden', 'false');

    /* Measure with provisional placement off-screen */
    el.style.left = '0px';
    el.style.top = '0px';
    el.style.maxWidth = Math.min(MAX_WIDTH, window.innerWidth - MARGIN * 2) + 'px';

    var rect = target.getBoundingClientRect();
    var tipRect = el.getBoundingClientRect();
    var vw = window.innerWidth;
    var vh = window.innerHeight;

    var preferBelow = target.closest('.hand-row') != null;

    var spaceAbove = rect.top - MARGIN;
    var spaceBelow = vh - rect.bottom - MARGIN;

    var placeBelow;
    if (preferBelow) {
      placeBelow = spaceBelow >= tipRect.height + GAP || spaceBelow >= spaceAbove;
    } else {
      placeBelow = spaceAbove < tipRect.height + GAP && spaceBelow > spaceAbove;
    }

    var top;
    if (placeBelow) {
      top = rect.bottom + GAP;
      if (top + tipRect.height > vh - MARGIN) {
        top = Math.max(MARGIN, vh - MARGIN - tipRect.height);
      }
      el.classList.add('tip-below');
      el.classList.remove('tip-above');
    } else {
      top = rect.top - tipRect.height - GAP;
      if (top < MARGIN) {
        top = MARGIN;
      }
      el.classList.add('tip-above');
      el.classList.remove('tip-below');
    }

    /* Center on target, then clamp horizontally into the viewport */
    var left = rect.left + rect.width / 2 - tipRect.width / 2;
    left = Math.max(MARGIN, Math.min(left, vw - MARGIN - tipRect.width));

    el.style.left = Math.round(left) + 'px';
    el.style.top = Math.round(top) + 'px';
  }

  function showFor(target) {
    if (!(target instanceof Element)) return;
    if (!target.classList.contains('has-tip')) return;
    var text = (target.getAttribute('data-tip') || '').trim();
    if (!text) return;

    clearTimers();
    activeTarget = target;
    showTimer = setTimeout(function () {
      showTimer = null;
      if (activeTarget === target && document.contains(target)) {
        place(target);
      }
    }, SHOW_DELAY);
  }

  function scheduleHide() {
    clearTimers();
    hideTimer = setTimeout(hide, HIDE_DELAY);
  }

  function onPointerOver(e) {
    var t = e.target;
    if (!(t instanceof Element)) return;
    var host = t.closest('.has-tip[data-tip]');
    if (!host) return;
    showFor(host);
  }

  function onPointerOut(e) {
    if (!activeTarget) return;
    var related = e.relatedTarget;
    if (related instanceof Node && activeTarget.contains(related)) return;
    var from = e.target;
    if (from instanceof Element) {
      var host = from.closest('.has-tip[data-tip]');
      if (host && host === activeTarget) {
        scheduleHide();
      }
    }
  }

  function onFocusIn(e) {
    var t = e.target;
    if (!(t instanceof Element)) return;
    if (t.classList.contains('has-tip') && t.hasAttribute('data-tip')) {
      showFor(t);
    }
  }

  function onFocusOut(e) {
    if (!activeTarget) return;
    var t = e.target;
    if (t === activeTarget) {
      scheduleHide();
    }
  }

  function onScrollOrResize() {
    if (activeTarget && tipEl && tipEl.classList.contains('is-visible')) {
      if (!document.contains(activeTarget)) {
        hide();
        return;
      }
      place(activeTarget);
    }
  }

  function onKeyDown(e) {
    if (e.key === 'Escape') hide();
  }

  function onDomChange() {
    if (activeTarget && !document.contains(activeTarget)) {
      hide();
    }
  }

  function bind() {
    if (bound) return;
    bound = true;
    document.addEventListener('pointerover', onPointerOver, true);
    document.addEventListener('pointerout', onPointerOut, true);
    document.addEventListener('focusin', onFocusIn, true);
    document.addEventListener('focusout', onFocusOut, true);
    document.addEventListener('keydown', onKeyDown, true);
    window.addEventListener('scroll', onScrollOrResize, true);
    window.addEventListener('resize', onScrollOrResize);
    /* Blazor re-renders replace nodes; drop stale tips */
    try {
      var mo = new MutationObserver(onDomChange);
      mo.observe(document.documentElement, { childList: true, subtree: true });
    } catch (_) {
      /* ignore */
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bind);
  } else {
    bind();
  }
})();
