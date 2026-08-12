import { PermissionEditorProvider } from './permission-editor-context.tsx';
import { labelForPermission } from './permission-labels.ts';
import { PermissionMatrix } from './permission-matrix.tsx';
import { usePermissionEditor } from './use-permission-editor.ts';
import {
    getGetPermissionsQueryKey,
    useGetPermissions,
    useResetPermissions,
    useUpdatePermissions,
} from '@/api/appointme.ts';
import { ConfirmDialog, useModalDialog } from '@/components/modal-dialog';
import { Button, InputGroup, InputGroupAddon, InputGroupInput } from '@/components/ui';
import { useNavigationGuard } from '@/hooks';
import { useQueryClient } from '@tanstack/react-query';
import { SearchIcon } from 'lucide-react';
import { parseAsString, useQueryState } from 'nuqs';
import { useMemo } from 'react';
import { toast } from 'sonner';

export const Permissions = () => {
    const queryClient = useQueryClient();
    const { data, isLoading } = useGetPermissions();
    const { mutateAsync: savePermissions, isPending: isSaving } = useUpdatePermissions();
    const { mutateAsync: resetPermissions, isPending: isResetting } = useResetPermissions();
    const editor = usePermissionEditor(data);
    const modalDialog = useModalDialog();

    const [search, setSearch] = useQueryState('search', parseAsString.withDefault(''));

    useNavigationGuard({
        when: editor.isDirty && !isSaving,
        title: 'Discard unsaved changes?',
        description: 'You have permission changes that have not been saved. Leaving this page will discard them.',
    });

    const filteredGroups = useMemo(() => {
        if (!data) {
            return [];
        }

        const needle = search.trim().toLowerCase();
        if (!needle) {
            return data.groups;
        }

        const matchesNeedle = (key: string) =>
            key.toLowerCase().includes(needle) || labelForPermission(key).toLowerCase().includes(needle);

        return data.groups
            .map(group => ({
                ...group,
                permissions: group.permissions.filter(permission => matchesNeedle(permission.key)),
            }))
            .filter(group => group.permissions.length > 0);
    }, [data, search]);

    const handleSave = async () => {
        await savePermissions({ data: { grants: editor.buildGrants() } });
        await queryClient.invalidateQueries({ queryKey: getGetPermissionsQueryKey() });
        editor.reset();
        toast.success('Permissions saved.');
    };

    const handleReset = async () => {
        const confirmed = await modalDialog.open<boolean>(props => (
            <ConfirmDialog
                {...props}
                title='Reset all permission overrides?'
                description='This removes every override for your company and restores roles to their default permissions. This cannot be undone.'
                confirmLabel='Reset'
            />
        ));
        if (!confirmed) {
            return;
        }

        await resetPermissions();
        await queryClient.invalidateQueries({ queryKey: getGetPermissionsQueryKey() });
        editor.reset();
        toast.success('Permissions reset to defaults.');
    };

    return (
        <div className='flex flex-col gap-6'>
            <div className='flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between'>
                <div>
                    <h1 className='text-xl font-semibold'>Role permissions</h1>
                    <p className='text-muted-foreground mt-1 text-sm'>
                        Control what each role can do within your company.
                    </p>
                </div>
                <div className='flex items-center justify-end gap-2'>
                    <Button
                        variant='outline'
                        size='sm'
                        disabled={isLoading || isSaving || isResetting}
                        onClick={handleReset}
                    >
                        Reset to defaults
                    </Button>
                    <Button size='sm' disabled={!editor.isDirty || isSaving || isResetting} onClick={handleSave}>
                        Save changes
                    </Button>
                </div>
            </div>

            <InputGroup className='max-w-sm'>
                <InputGroupAddon>
                    <SearchIcon />
                </InputGroupAddon>
                <InputGroupInput
                    id='permission-search'
                    name='permission-search'
                    type='search'
                    aria-label='Search permissions'
                    placeholder='Search permissions'
                    value={search}
                    onChange={({ target: { value } }) => setSearch(value)}
                />
            </InputGroup>

            <PermissionEditorProvider value={editor}>
                <PermissionMatrix roles={data?.roles ?? []} groups={filteredGroups} isLoading={isLoading} />
            </PermissionEditorProvider>
        </div>
    );
};
