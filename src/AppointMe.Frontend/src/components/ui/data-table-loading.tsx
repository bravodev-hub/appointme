import { type DataTableFeatures } from '@/components/ui/data-table-features';
import { Skeleton } from '@/components/ui/skeleton.tsx';
import { TableCell, TableRow } from '@/components/ui/table.tsx';
import { Column, RowData } from '@tanstack/react-table';

interface DataTableLoadingProps<TData extends RowData> {
    columns: Column<DataTableFeatures, TData, unknown>[];
    rows?: number;
}

export function DataTableLoading<TData extends RowData>({ columns, rows = 5 }: DataTableLoadingProps<TData>) {
    return (
        <>
            {Array.from({ length: rows }).map((_, i) => (
                <TableRow key={`skeleton-${i}`}>
                    {columns.map(column => (
                        <TableCell key={column.id}>
                            <Skeleton className='h-4 w-3/4' />
                        </TableCell>
                    ))}
                </TableRow>
            ))}
        </>
    );
}
