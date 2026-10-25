import {
    DataTableEmpty,
    DataTableLoading,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from '@/components/ui';
import { type DataTableFeatures } from '@/components/ui/data-table-features';
import { cn } from '@/lib/utils';
import {
    type CellData,
    Table as ReactTable,
    Row,
    RowData,
    type TableFeatures,
    flexRender,
} from '@tanstack/react-table';
import { ReactNode } from 'react';

declare module '@tanstack/react-table' {
    // The type parameters must match TanStack's declaration to merge, even though unused here.
    interface ColumnMeta<
        in out TFeatures extends TableFeatures,
        in out TData extends RowData,
        TValue extends CellData = CellData,
    > {
        className?: string;
        /** Overrides `className` for header cells only (e.g. different sticky background). */
        headerClassName?: string;
    }
}

interface DataTableProps<TData extends RowData> {
    table: ReactTable<DataTableFeatures, TData>;
    isLoading?: boolean;
    isFetching?: boolean;
    skeletonRows?: number;
    emptyMessage?: string;
    className?: string;
    onRowClick?: (row: Row<DataTableFeatures, TData>) => void;
    /**
     * Custom row renderer. Return a ReactNode to replace the default row rendering,
     * or return `undefined` to fall back to the default rendering.
     */
    renderRow?: (row: Row<DataTableFeatures, TData>) => ReactNode | undefined;
}

export function DataTable<TData extends RowData>({
    table,
    isLoading = false,
    isFetching = false,
    skeletonRows,
    emptyMessage,
    className,
    onRowClick,
    renderRow,
}: DataTableProps<TData>) {
    const visibleColumns = table.getVisibleFlatColumns();
    const rows = table.getRowModel().rows;
    const showOverlay = isFetching && !isLoading;

    return (
        <div className={cn('overflow-auto rounded-md border', className)}>
            <Table>
                <TableHeader className='bg-muted sticky top-0 z-10'>
                    {table.getHeaderGroups().map(headerGroup => (
                        <TableRow key={headerGroup.id}>
                            {headerGroup.headers.map(header => (
                                <TableHead
                                    key={header.id}
                                    className={
                                        header.column.columnDef.meta?.headerClassName ??
                                        header.column.columnDef.meta?.className
                                    }
                                >
                                    {header.isPlaceholder
                                        ? null
                                        : flexRender(header.column.columnDef.header, header.getContext())}
                                </TableHead>
                            ))}
                        </TableRow>
                    ))}
                </TableHeader>
                <TableBody className={cn('transition-opacity duration-150', showOverlay && 'opacity-60')}>
                    {isLoading ? (
                        <DataTableLoading columns={visibleColumns} rows={skeletonRows} />
                    ) : rows.length ? (
                        rows.map(row => {
                            const custom = renderRow?.(row);
                            if (custom !== undefined) return custom;
                            return (
                                <TableRow
                                    key={row.id}
                                    data-state={row.getIsSelected() ? 'selected' : undefined}
                                    onClick={() => onRowClick?.(row)}
                                    className={cn(onRowClick && 'cursor-pointer')}
                                >
                                    {row.getVisibleCells().map(cell => (
                                        <TableCell key={cell.id} className={cell.column.columnDef.meta?.className}>
                                            {flexRender(cell.column.columnDef.cell, cell.getContext())}
                                        </TableCell>
                                    ))}
                                </TableRow>
                            );
                        })
                    ) : (
                        <DataTableEmpty colSpan={visibleColumns.length} message={emptyMessage} />
                    )}
                </TableBody>
            </Table>
        </div>
    );
}
