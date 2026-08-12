import { ConfirmDialog, useModalDialog } from '@/components/modal-dialog';
import { useEffect } from 'react';
import { useBlocker } from 'react-router';

interface DirtyNavigationGuardOptions {
    when: boolean;
    title: string;
    description: string;
    confirmLabel?: string;
    cancelLabel?: string;
}

export const useNavigationGuard = ({
    when,
    title,
    description,
    confirmLabel = 'Discard',
    cancelLabel = 'Stay',
}: DirtyNavigationGuardOptions): void => {
    const modalDialog = useModalDialog();

    const blocker = useBlocker(
        ({ currentLocation, nextLocation }) => when && currentLocation.pathname !== nextLocation.pathname,
    );

    useEffect(() => {
        if (blocker.state !== 'blocked') {
            return;
        }

        let cancelled = false;
        modalDialog
            .open<boolean>(props => (
                <ConfirmDialog
                    {...props}
                    title={title}
                    description={description}
                    confirmLabel={confirmLabel}
                    cancelLabel={cancelLabel}
                />
            ))
            .then(confirmed => {
                if (cancelled) {
                    return;
                }

                if (confirmed) {
                    blocker.proceed?.();
                } else {
                    blocker.reset?.();
                }
            });

        return () => {
            cancelled = true;
        };
    }, [blocker, modalDialog, title, description, confirmLabel, cancelLabel]);
};
