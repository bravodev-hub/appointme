import { PermissionEditor, PermissionEditorContext } from './use-permission-editor-context';
import { ReactNode } from 'react';

interface PermissionEditorProviderProps {
    value: PermissionEditor;
    children: ReactNode;
}

export const PermissionEditorProvider = ({ value, children }: Readonly<PermissionEditorProviderProps>) => (
    <PermissionEditorContext.Provider value={value}>{children}</PermissionEditorContext.Provider>
);
