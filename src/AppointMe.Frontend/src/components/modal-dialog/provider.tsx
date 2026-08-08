import { ModalDialogContext, ModalFactory } from './context';
import { PropsWithChildren, useCallback, useMemo, useState } from 'react';

const ANIMATION_DURATION_MS = 200;

interface IModalInstance<T> {
    id: string;
    open: boolean;
    resolve: (value: T | PromiseLike<T>) => void;
    modalFactory: ModalFactory<T>;
}

export const ModalDialogProvider = ({ children }: PropsWithChildren) => {
    const [modals, setModals] = useState<IModalInstance<unknown>[]>([]);

    const close = useCallback((id: string) => {
        setModals(modals => modals.map(modal => (modal.id === id ? { ...modal, open: false } : modal)));
        setTimeout(() => {
            setModals(modals => modals.filter(modal => modal.id !== id));
        }, ANIMATION_DURATION_MS);
    }, []);

    const open = useCallback(
        <TResult,>(modalFactory: ModalFactory<TResult>) => {
            return new Promise<TResult>(resolve => {
                const id = crypto.randomUUID();
                const instance: IModalInstance<TResult> = {
                    id,
                    open: true,
                    modalFactory,
                    resolve: value => {
                        close(id);
                        resolve(value);
                    },
                };
                setModals(modals => [...modals, instance as IModalInstance<unknown>]);
            });
        },
        [close],
    );

    const contextValue = useMemo(() => ({ open }), [open]);

    return (
        <ModalDialogContext.Provider value={contextValue}>
            {children}
            {modals.map(({ id, modalFactory: Content, resolve, open: visible }) => (
                <Content key={id} resolve={resolve} visible={visible} />
            ))}
        </ModalDialogContext.Provider>
    );
};
