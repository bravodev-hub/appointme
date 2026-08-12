import { Columns } from './columns';
import { getGetCustomersQueryKey, useDeleteCustomer, useGetCustomers } from '@/api/appointme';
import { Can } from '@/components/auth/can';
import { ConfirmDialog, useModalDialog } from '@/components/modal-dialog';
import {
    Button,
    DataTable,
    DataTablePagination,
    DataTableViewOptions,
    InputGroup,
    InputGroupAddon,
    InputGroupInput,
} from '@/components/ui';
import { useDebouncedValue } from '@tanstack/react-pacer/debouncer';
import { keepPreviousData, useQueryClient } from '@tanstack/react-query';
import { RowSelectionState, VisibilityState, getCoreRowModel, useReactTable } from '@tanstack/react-table';
import { PlusIcon, SearchIcon, Trash2Icon } from 'lucide-react';
import { parseAsString, useQueryState } from 'nuqs';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';

export const Customers = () => {
    const navigate = useNavigate();
    const modalDialog = useModalDialog();
    const queryClient = useQueryClient();
    const [rowSelection, setRowSelection] = useState<RowSelectionState>({});
    const [columnVisibility, setColumnVisibility] = useState<VisibilityState>({});
    const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 10 });

    const { mutateAsync: deleteCustomer } = useDeleteCustomer();

    const handleBulkDelete = async () => {
        const selectedRows = table.getSelectedRowModel().rows;
        const confirmed = await modalDialog.open<boolean>(props => (
            <ConfirmDialog
                {...props}
                title='Delete customers'
                description={`${selectedRows.length} ${selectedRows.length === 1 ? 'customer' : 'customers'} will be permanently deleted and cannot be recovered.`}
                confirmLabel='Delete'
            />
        ));
        if (!confirmed) {
            return;
        }

        await Promise.all(selectedRows.map(row => deleteCustomer({ id: row.original.id })));
        await queryClient.invalidateQueries({ queryKey: getGetCustomersQueryKey() });
        setRowSelection({});
        toast.success(`${selectedRows.length} ${selectedRows.length === 1 ? 'customer' : 'customers'} deleted.`);
    };
    const [search, setSearch] = useQueryState('search', parseAsString.withDefault(''));
    const [debouncedSearch] = useDebouncedValue(search, {
        wait: 400,
        onExecute: () => setPagination(p => ({ ...p, pageIndex: 0 })),
    });

    const {
        data: response,
        isLoading,
        isFetching,
    } = useGetCustomers(
        {
            search: debouncedSearch,
            page: pagination.pageIndex + 1,
            limit: pagination.pageSize,
        },
        { query: { placeholderData: keepPreviousData } },
    );

    // eslint-disable-next-line react-hooks/incompatible-library -- TanStack Table is on the React Compiler's incompatible-library list; the compiler skips this component either way
    const table = useReactTable({
        data: response?.customers.items ?? [],
        columns: Columns,
        state: {
            columnVisibility,
            rowSelection,
            pagination,
        },
        getRowId: row => row.id,
        enableRowSelection: true,
        onRowSelectionChange: setRowSelection,
        onColumnVisibilityChange: setColumnVisibility,
        onPaginationChange: setPagination,
        getCoreRowModel: getCoreRowModel(),
        manualPagination: true,
        rowCount: response?.customers.totalCount,
    });

    return (
        <div className='flex w-full flex-col gap-4'>
            <div className='flex items-center gap-2'>
                <InputGroup className='max-w-sm'>
                    <InputGroupAddon>
                        <SearchIcon />
                    </InputGroupAddon>
                    <InputGroupInput
                        id='customer-search'
                        name='customer-search'
                        type='search'
                        aria-label='Search customers'
                        placeholder='Search by name, email...'
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                    />
                </InputGroup>
                <div className='ms-auto flex items-center gap-2'>
                    <DataTableViewOptions table={table} />
                    {table.getSelectedRowModel().rows.length > 0 ? (
                        <Can permission='customers:delete'>
                            <Button variant='destructive' size='sm' onClick={handleBulkDelete} aria-label='Delete'>
                                <Trash2Icon />
                                <span className='hidden sm:inline'>Delete</span>
                            </Button>
                        </Can>
                    ) : (
                        <Can permission='customers:create'>
                            <Button asChild size='sm'>
                                <Link to='/customers/new' aria-label='Add customer'>
                                    <PlusIcon />
                                    <span className='hidden sm:inline'>Add customer</span>
                                </Link>
                            </Button>
                        </Can>
                    )}
                </div>
            </div>
            <DataTable
                table={table}
                isLoading={isLoading}
                isFetching={isFetching}
                onRowClick={row => navigate(`/customers/${row.original.id}`)}
            />
            <DataTablePagination table={table} />
        </div>
    );
};
