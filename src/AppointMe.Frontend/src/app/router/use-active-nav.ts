import { useMatches } from 'react-router';

interface NavHandle {
    navId?: string;
}

export const useActiveNavIds = (): ReadonlySet<string> => {
    const matches = useMatches();

    return new Set(
        matches
            .map(match => (match.handle as NavHandle | undefined)?.navId)
            .filter((id): id is string => Boolean(id)),
    );
};
