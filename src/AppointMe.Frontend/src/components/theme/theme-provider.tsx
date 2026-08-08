import { Theme, ThemeProviderContext } from './use-theme';
import { STORAGE_KEYS } from '@/lib/storage-keys.ts';
import { ReactNode, useCallback, useEffect, useMemo, useState } from 'react';

const isTheme = (value: unknown): value is Theme => value === 'light' || value === 'dark';

type ThemeProviderProps = {
    children: ReactNode;
    defaultTheme?: Theme;
};

export function ThemeProvider({ children, defaultTheme = 'light', ...props }: ThemeProviderProps) {
    const [theme, setTheme] = useState<Theme>(() => {
        const stored = globalThis.localStorage.getItem(STORAGE_KEYS.theme);
        return isTheme(stored) ? stored : defaultTheme;
    });

    useEffect(() => {
        const root = globalThis.document.documentElement;

        root.classList.remove('light', 'dark');
        root.classList.add(theme);
    }, [theme]);

    const setThemePersisted = useCallback((theme: Theme) => {
        localStorage.setItem(STORAGE_KEYS.theme, theme);
        setTheme(theme);
    }, []);

    const value = useMemo(
        () => ({
            theme,
            setTheme: setThemePersisted,
        }),
        [theme, setThemePersisted],
    );

    return (
        <ThemeProviderContext.Provider {...props} value={value}>
            {children}
        </ThemeProviderContext.Provider>
    );
}
