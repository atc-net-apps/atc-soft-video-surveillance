// The web client's language, kept in this browser (see LanguageChoice).
window.atcLanguage = {
    get: () => localStorage.getItem("language"),
    set: culture => localStorage.setItem("language", culture),
    browser: () => navigator.language
};