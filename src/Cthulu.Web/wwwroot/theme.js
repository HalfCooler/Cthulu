// 阳间 / 阴间主题：在首屏绘制前由内联 init 调用，运行时由 MainLayout 切换。
window.cthuluTheme = {
    key: "cthulu.theme",
    normalize(t) {
        return t === "yang" || t === "yin" ? t : "yin";
    },
    apply(t) {
        t = this.normalize(t);
        document.documentElement.setAttribute("data-theme", t);
        try {
            localStorage.setItem(this.key, t);
        } catch {
            /* private mode etc. */
        }
        return t;
    },
    init() {
        let t = "yin";
        try {
            t = localStorage.getItem(this.key) || "yin";
        } catch {
            /* ignore */
        }
        return this.apply(t);
    },
    get() {
        return this.normalize(document.documentElement.getAttribute("data-theme"));
    },
    toggle() {
        return this.apply(this.get() === "yin" ? "yang" : "yin");
    }
};
