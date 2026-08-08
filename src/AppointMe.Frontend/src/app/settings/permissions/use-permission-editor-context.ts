import { PermissionDto, Role, RoleDefinitionDto, RolePermissionGrantDto } from '@/api/appointme.schemas.ts';
import { createContext, use } from 'react';

export interface PermissionCellState {
    isGranted: boolean;
    isEditable: boolean;
    state: 'default' | 'overridden' | 'modified';
}

export interface PermissionEditor {
    toggle: (permKey: string, role: Role, next: boolean) => void;
    isDirty: boolean;
    reset: () => void;
    buildGrants: () => RolePermissionGrantDto[];
    getCellState: (permission: PermissionDto, role: RoleDefinitionDto) => PermissionCellState;
}

export const PermissionEditorContext = createContext<PermissionEditor | null>(null);

export const usePermissionEditorContext = () => {
    const context = use(PermissionEditorContext);
    if (!context) {
        throw new Error('usePermissionEditorContext must be used within a PermissionEditorProvider');
    }
    return context;
};
