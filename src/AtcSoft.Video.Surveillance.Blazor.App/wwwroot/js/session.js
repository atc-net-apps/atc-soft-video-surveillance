// The signed-in session of this browser: the refresh token and who it is for,
// so a page that reloads (a language change) is still signed in. Every tab of
// the site shares it.
window.atcSession = {
    get: () => localStorage.getItem("session"),
    set: value => localStorage.setItem("session", value),
    clear: () => localStorage.removeItem("session"),

    // Forgets the session only while it is still the one given: another tab
    // may have saved a newer one, which stays.
    clearIf: expected => {
        if (localStorage.getItem("session") === expected) {
            localStorage.removeItem("session");
        }
    },

    // Tells the app when another tab forgets the session (signs out).
    watch: target => window.addEventListener("storage", e => {
        if ((e.key === "session" || e.key === null) && e.newValue === null) {
            target.invokeMethodAsync("OnSignedOutElsewhere");
        }
    })
};