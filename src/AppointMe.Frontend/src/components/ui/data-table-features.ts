import {
    columnVisibilityFeature,
    rowPaginationFeature,
    rowSelectionFeature,
    tableFeatures,
} from '@tanstack/react-table';

export const dataTableFeatures = tableFeatures({ columnVisibilityFeature, rowPaginationFeature, rowSelectionFeature });

export type DataTableFeatures = typeof dataTableFeatures;
