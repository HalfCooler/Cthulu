/**
 * System TTS via Web Speech API (speechSynthesis).
 * Used by Room event-log 播报 and turn-action announce.
 */
(function () {
  'use strict';

  let activeDotNet = null;
  let resumeTimer = null;
  let expectedEnd = false;

  function clearResumeTimer() {
    if (resumeTimer != null) {
      clearInterval(resumeTimer);
      resumeTimer = null;
    }
  }

  function notifyEnded() {
    const ref = activeDotNet;
    activeDotNet = null;
    clearResumeTimer();
    if (ref) {
      try {
        ref.invokeMethodAsync('OnLogTtsEnded');
      } catch (_) {
        /* component may already be disposed */
      }
    }
  }

  function pickChineseVoice() {
    try {
      const voices = speechSynthesis.getVoices() || [];
      return (
        voices.find((v) => /^zh(-|_)/i.test(v.lang) && /CN|Hans|Chinese/i.test(v.lang + v.name)) ||
        voices.find((v) => /^zh/i.test(v.lang)) ||
        null
      );
    } catch {
      return null;
    }
  }

  /**
   * Chrome sometimes freezes speechSynthesis; nudge with resume().
   */
  function startResumeWatch() {
    clearResumeTimer();
    resumeTimer = setInterval(() => {
      try {
        if (!speechSynthesis.speaking) {
          clearResumeTimer();
          return;
        }
        if (speechSynthesis.paused) {
          speechSynthesis.resume();
        }
      } catch {
        clearResumeTimer();
      }
    }, 2500);
  }

  window.cthuluTts = {
    /**
     * Short one-shot announcement (e.g. turn reminder).
     * Stops any ongoing speech (including log TTS) first; does not attach a DotNet callback.
     * @param {string} text
     * @param {string} [lang]
     * @returns {{ ok: boolean, error?: string }}
     */
    announce(text, lang) {
      if (!('speechSynthesis' in window) || typeof SpeechSynthesisUtterance === 'undefined') {
        return { ok: false, error: '当前浏览器不支持系统语音播报' };
      }

      const message = String(text ?? '').trim();
      if (!message) {
        return { ok: false, error: '无播报内容' };
      }

      // End log TTS cleanly so Blazor clears speaking state.
      this.stop(true);

      const voiceLang = lang || 'zh-CN';
      const voice = pickChineseVoice();
      const u = new SpeechSynthesisUtterance(message);
      u.lang = voiceLang;
      u.rate = 1.05;
      u.pitch = 1;
      if (voice) u.voice = voice;

      try {
        speechSynthesis.speak(u);
        startResumeWatch();
        return { ok: true };
      } catch (e) {
        return { ok: false, error: (e && e.message) || '语音播报失败' };
      }
    },

    /**
     * @param {string[]} lines  Messages to speak in order
     * @param {string} [lang]   BCP-47 language tag, default zh-CN
     * @param {*} [dotNetRef]   DotNetObjectReference for OnLogTtsEnded
     * @returns {{ ok: boolean, error?: string }}
     */
    speak(lines, lang, dotNetRef) {
      if (!('speechSynthesis' in window) || typeof SpeechSynthesisUtterance === 'undefined') {
        return { ok: false, error: '当前浏览器不支持系统语音播报' };
      }

      const texts = (Array.isArray(lines) ? lines : [lines])
        .map((t) => String(t ?? '').trim())
        .filter((t) => t.length > 0);

      if (texts.length === 0) {
        return { ok: false, error: '暂无事件可播报' };
      }

      this.stop(false);
      activeDotNet = dotNetRef || null;
      expectedEnd = false;

      const voiceLang = lang || 'zh-CN';
      const voice = pickChineseVoice();
      let remaining = texts.length;
      let failed = false;

      const onOneDone = (fromError) => {
        if (expectedEnd) return;
        remaining -= 1;
        if (fromError) failed = true;
        if (remaining <= 0) {
          expectedEnd = true;
          notifyEnded();
        }
      };

      for (const text of texts) {
        const u = new SpeechSynthesisUtterance(text);
        u.lang = voiceLang;
        u.rate = 1;
        u.pitch = 1;
        if (voice) u.voice = voice;

        u.onend = () => onOneDone(false);
        u.onerror = (ev) => {
          const err = ev && ev.error;
          // cancel() produces interrupted/canceled — treat as intentional stop
          if (err === 'interrupted' || err === 'canceled') {
            if (!expectedEnd) {
              expectedEnd = true;
              notifyEnded();
            }
            return;
          }
          onOneDone(true);
        };

        speechSynthesis.speak(u);
      }

      startResumeWatch();
      return { ok: true };
    },

    /**
     * @param {boolean} [notify=true] Whether to notify Blazor of end state
     */
    stop(notify) {
      const shouldNotify = notify !== false;
      expectedEnd = true;
      clearResumeTimer();
      try {
        if ('speechSynthesis' in window) {
          speechSynthesis.cancel();
        }
      } catch {
        /* ignore */
      }
      if (shouldNotify) {
        notifyEnded();
      } else {
        activeDotNet = null;
      }
    },

    isSpeaking() {
      try {
        return !!(window.speechSynthesis && (speechSynthesis.speaking || speechSynthesis.pending));
      } catch {
        return false;
      }
    }
  };

  // Some browsers load voices asynchronously
  if ('speechSynthesis' in window) {
    try {
      speechSynthesis.getVoices();
      speechSynthesis.addEventListener('voiceschanged', () => {
        speechSynthesis.getVoices();
      });
    } catch {
      /* ignore */
    }
  }
})();
