const STORAGE_KEY = "keylet-theme";
const COOKIE_KEY = "keylet-theme";
const AUTO_THEME = "auto";
const LIGHT_THEME = "light";
const DARK_THEME = "dark";

const root = document.documentElement;
const button = document.querySelector("[data-theme-toggle]");

if (button) {
    function normalizeTheme(theme) {
        return theme === LIGHT_THEME || theme === DARK_THEME ? theme : AUTO_THEME;
    }

    function readStoredTheme() {
        try {
            return localStorage.getItem(STORAGE_KEY);
        } catch {
            return null;
        }
    }

    function persistTheme(theme) {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch {
            // Continue with the cookie and current document state when storage is unavailable.
        }

        document.cookie = `${COOKIE_KEY}=${encodeURIComponent(theme)}; Path=/; Max-Age=31536000; SameSite=Lax`;
    }

    function themeName(theme) {
        return theme === LIGHT_THEME ? "Light" : theme === DARK_THEME ? "Dark" : "Auto";
    }

    function nextTheme(theme) {
        return theme === AUTO_THEME ? LIGHT_THEME : theme === LIGHT_THEME ? DARK_THEME : AUTO_THEME;
    }

    function applyTheme(theme) {
        const normalizedTheme = normalizeTheme(theme);

        if (normalizedTheme === AUTO_THEME) {
            root.removeAttribute("data-theme");
        } else {
            root.dataset.theme = normalizedTheme;
        }

        const label = `Theme: ${themeName(normalizedTheme)}. Switch to ${themeName(nextTheme(normalizedTheme))}.`;
        button.dataset.theme = normalizedTheme;
        button.title = label;
        button.setAttribute("aria-label", label);
        return normalizedTheme;
    }

    let theme = normalizeTheme(readStoredTheme() || root.dataset.theme);
    persistTheme(theme);
    theme = applyTheme(theme);

    button.addEventListener("click", () => {
        theme = nextTheme(theme);
        persistTheme(theme);
        applyTheme(theme);
    });
}
