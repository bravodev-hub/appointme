import { UIMatch, useMatches } from 'react-router-dom';

export interface BreadcrumbItem {
    title: string;
    url?: string;
}

interface BreadcrumbHandle {
    breadcrumb: string | ((match: UIMatch) => string);
    group?: boolean;
}

type BreadcrumbMatch = UIMatch & {
    handle: BreadcrumbHandle;
};

function isBreadcrumbMatch(match: UIMatch): match is BreadcrumbMatch {
    return typeof match.handle === 'object' && match.handle !== null && 'breadcrumb' in match.handle;
}

export const useBreadcrumbs = (): BreadcrumbItem[] => {
    const matches = useMatches();

    return matches.filter(isBreadcrumbMatch).map(match => {
        const title =
            typeof match.handle.breadcrumb === 'function' ? match.handle.breadcrumb(match) : match.handle.breadcrumb;

        return {
            title,
            url: match.handle.group ? undefined : match.pathname,
        };
    });
};
