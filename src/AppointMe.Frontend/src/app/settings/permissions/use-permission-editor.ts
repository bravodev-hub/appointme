import { PermissionCellState, PermissionEditor } from './use-permission-editor-context.ts';
import { GetPermissionsResponse, Role } from '@/api/appointme.schemas.ts';
import { useCallback, useMemo, useState } from 'react';

const LOCKED_CELL_STATE: PermissionCellState = { isGranted: true, isEditable: false, state: 'default' };

const cellKey = (permKey: string, role: string) => `${permKey}\x00${role}`;

const resolveState = (
    isGranted: boolean,
    serverGranted: boolean,
    isOverridden: boolean,
): PermissionCellState['state'] => {
    if (isGranted !== serverGranted) {
        return 'modified';
    }

    if (isOverridden) {
        return 'overridden';
    }

    return 'default';
};

interface PermissionEdit {
    permKey: string;
    role: Role;
    isGranted: boolean;
}

export const usePermissionEditor = (data: GetPermissionsResponse | undefined): PermissionEditor => {
    const [edited, setEdited] = useState<Map<string, PermissionEdit>>(new Map());

    const serverGrants = useMemo(() => {
        const map = new Map<string, boolean>();
        for (const group of data?.groups ?? []) {
            for (const permission of group.permissions) {
                for (const role of Object.keys(permission.grants)) {
                    map.set(cellKey(permission.key, role), permission.grants[role]?.isGranted ?? false);
                }
            }
        }
        return map;
    }, [data]);

    const getValue = useCallback(
        (permKey: string, role: string): boolean => {
            const key = cellKey(permKey, role);
            return edited.get(key)?.isGranted ?? serverGrants.get(key) ?? false;
        },
        [edited, serverGrants],
    );

    const [trackedServerGrants, setTrackedServerGrants] = useState(serverGrants);
    if (trackedServerGrants !== serverGrants) {
        setTrackedServerGrants(serverGrants);
        setEdited(prev => {
            const next = new Map(prev);
            for (const [key, edit] of prev) {
                if ((serverGrants.get(key) ?? false) === edit.isGranted) {
                    next.delete(key);
                }
            }
            return next.size === prev.size ? prev : next;
        });
    }

    const toggle = useCallback<PermissionEditor['toggle']>(
        (permKey, role, next) => {
            const key = cellKey(permKey, role);
            const serverValue = serverGrants.get(key) ?? false;
            setEdited(prev => {
                const nextEdited = new Map(prev);
                if (next === serverValue) {
                    nextEdited.delete(key);
                } else {
                    nextEdited.set(key, { permKey, role, isGranted: next });
                }
                return nextEdited;
            });
        },
        [serverGrants],
    );

    const isDirty = edited.size > 0;

    const reset = useCallback(() => setEdited(new Map()), []);

    const buildGrants = useCallback<PermissionEditor['buildGrants']>(
        () =>
            Array.from(edited.values()).map(({ permKey, role, isGranted }) => ({
                permissionKey: permKey,
                role,
                isGranted,
            })),
        [edited],
    );

    const getCellState = useCallback<PermissionEditor['getCellState']>(
        (permission, role) => {
            if (role.allowsPermissionOverrides) {
                const grant = permission.grants[role.role];
                const serverGranted = grant?.isGranted ?? false;
                const isOverridden = grant?.isOverridden ?? false;
                const isGranted = getValue(permission.key, role.role);
                return { isGranted, isEditable: true, state: resolveState(isGranted, serverGranted, isOverridden) };
            }

            return LOCKED_CELL_STATE;
        },
        [getValue],
    );

    return { toggle, isDirty, reset, buildGrants, getCellState };
};
