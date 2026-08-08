import { Columns } from './columns';
import { InviteEmployeeDialog } from './invite-employee-dialog';
import { TeamMemberType } from '@/api/appointme.schemas.ts';
import { getGetTeamQueryKey, useCancelInvitation, useDeleteEmployee, useGetTeam } from '@/api/appointme.ts';
import { usePermission } from '@/components/auth';
import { Can } from '@/components/auth/can';
import { ConfirmDialog, useModalDialog } from '@/components/modal-dialog';
import {
    Button,
    DataTable,
    DataTablePagination,
    InputGroup,
    InputGroupAddon,
    InputGroupInput,
    Separator,
} from '@/components/ui';
import { useDebouncedValue } from '@tanstack/react-pacer/debouncer';
import { keepPreviousData, useQueryClient } from '@tanstack/react-query';
import { RowSelectionState, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { PlusIcon, SearchIcon, Trash2Icon } from 'lucide-react';
import { parseAsString, useQueryState } from 'nuqs';
import { Fragment, useState } from 'react';
import { toast } from 'sonner';

export const Team = () => {
    const modalDialog = useModalDialog();
    const queryClient = useQueryClient();
    const { mutateAsync: cancelInvitation } = useCancelInvitation();
    const { mutateAsync: deleteEmployee } = useDeleteEmployee();
    const canRemove = usePermission('employees:remove');
    const [rowSelection, setRowSelection] = useState<RowSelectionState>({});
    const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 10 });
    const [search, setSearch] = useQueryState('search', parseAsString.withDefault(''));
    const [debouncedSearch] = useDebouncedValue(search, {
        wait: 400,
        onExecute: () => {
            setPagination({ ...pagination, pageIndex: 0 });
            setRowSelection({});
        },
    });

    const {
        data: team,
        isLoading,
        isFetching,
    } = useGetTeam(
        {
            page: pagination.pageIndex + 1,
            limit: pagination.pageSize,
            search: debouncedSearch,
        },
        {
            query: { placeholderData: keepPreviousData },
        },
    );

    // eslint-disable-next-line react-hooks/incompatible-library -- TanStack Table is on the React Compiler's incompatible-library list; the compiler skips this component either way
    const table = useReactTable({
        data: team?.members.items ?? [],
        columns: Columns,
        state: {
            rowSelection,
            pagination,
        },
        getRowId: row => row.id,
        enableRowSelection: row => canRemove && !row.original.isCurrentUser,
        onRowSelectionChange: setRowSelection,
        onPaginationChange: setPagination,
        getCoreRowModel: getCoreRowModel(),
        manualPagination: true,
        rowCount: team?.members.totalCount,
    });

    const handleBulkRemove = async () => {
        const selectedRows = table.getSelectedRowModel().rows;
        const confirmed = await modalDialog.open<boolean>(props => (
            <ConfirmDialog
                {...props}
                title='Remove member(s) from company'
                description='The following member(s) will no longer be able to access this company.'
                confirmLabel='Remove'
            >
                <ul className='flex flex-col gap-2 text-sm'>
                    {selectedRows.map((row, index) => (
                        <Fragment key={row.id}>
                            <li>{row.original.fullName ?? row.original.email}</li>
                            {index < selectedRows.length - 1 && <Separator />}
                        </Fragment>
                    ))}
                </ul>
            </ConfirmDialog>
        ));

        if (!confirmed) {
            return;
        }

        try {
            await Promise.all(
                selectedRows.map(row =>
                    row.original.type === TeamMemberType.Invitation
                        ? cancelInvitation({ id: row.original.id })
                        : deleteEmployee({ id: row.original.id }),
                ),
            );

            await queryClient.invalidateQueries({ queryKey: getGetTeamQueryKey() });
            setRowSelection({});

            const count = selectedRows.length;
            toast.success(`${count} ${count === 1 ? 'member' : 'members'} removed successfully.`);
        } catch {
            toast.error('Something went wrong. Please try again.');
        }
    };

    return (
        <div className='flex w-full flex-col gap-4'>
            <div className='flex items-center gap-3'>
                <InputGroup className='max-w-sm'>
                    <InputGroupAddon>
                        <SearchIcon />
                    </InputGroupAddon>
                    <InputGroupInput
                        placeholder='Search by name, email...'
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                    />
                </InputGroup>
                <div className='ml-auto'>
                    {table.getSelectedRowModel().rows.length > 0 ? (
                        <Can permission='employees:remove'>
                            <Button variant='destructive' size='sm' onClick={handleBulkRemove} aria-label='Remove'>
                                <Trash2Icon />
                                <span className='hidden sm:inline'>Remove</span>
                            </Button>
                        </Can>
                    ) : (
                        <Can permission='employees:invite'>
                            <Button
                                size='sm'
                                onClick={() => modalDialog.open(InviteEmployeeDialog)}
                                aria-label='Add employee'
                            >
                                <PlusIcon />
                                <span className='hidden sm:inline'>Add employee</span>
                            </Button>
                        </Can>
                    )}
                </div>
            </div>
            <DataTable table={table} isLoading={isLoading} isFetching={isFetching} />
            <DataTablePagination table={table} />
        </div>
    );
};
