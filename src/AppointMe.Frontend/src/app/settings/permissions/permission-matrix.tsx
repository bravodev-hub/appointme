import { PermissionGroupSection } from './permission-group-section.tsx';
import { PermissionGroupDto, RoleDefinitionDto } from '@/api/client.schemas.ts';
import {
    Skeleton,
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
    Tooltip,
    TooltipContent,
    TooltipTrigger,
} from '@/components/ui';
import { LockIcon } from 'lucide-react';

interface PermissionMatrixProps {
    roles: RoleDefinitionDto[];
    groups: PermissionGroupDto[];
    isLoading: boolean;
}

const SKELETON_PLACEHOLDER_ROLE_COUNT = 4;
const SKELETON_ROW_COUNT = 8;

export const PermissionMatrix = ({ roles, groups, isLoading }: Readonly<PermissionMatrixProps>) => {
    return (
        <div className='overflow-auto rounded-md border'>
            <Table>
                <TableHeader>
                    <TableRow>
                        <TableHead className='bg-muted sticky left-0 z-10 w-72'>Permission</TableHead>
                        {isLoading && roles.length === 0
                            ? Array.from({ length: SKELETON_PLACEHOLDER_ROLE_COUNT }).map((_, index) => (
                                  <TableHead key={index} className='min-w-32.5 text-center'>
                                      <Skeleton className='mx-auto h-4 w-16' />
                                  </TableHead>
                              ))
                            : roles.map(roleDto => (
                                  <TableHead key={roleDto.role} className='min-w-32.5 text-center'>
                                      {roleDto.allowsPermissionOverrides ? (
                                          roleDto.role
                                      ) : (
                                          <Tooltip>
                                              <TooltipTrigger asChild>
                                                  <span className='inline-flex cursor-default items-center gap-1.5'>
                                                      {roleDto.role}
                                                      <LockIcon className='text-muted-foreground size-3' />
                                                  </span>
                                              </TooltipTrigger>
                                              <TooltipContent className='max-w-48 text-center'>
                                                  This role always has full access and cannot be restricted.
                                              </TooltipContent>
                                          </Tooltip>
                                      )}
                                  </TableHead>
                              ))}
                    </TableRow>
                </TableHeader>
                {isLoading ? (
                    <TableBody>
                        {Array.from({ length: SKELETON_ROW_COUNT }).map((_, rowIndex) => (
                            <TableRow key={rowIndex}>
                                <TableCell className='bg-background sticky left-0 z-10 w-72 pl-8'>
                                    <Skeleton className='h-4 w-32' />
                                </TableCell>
                                {Array.from({ length: SKELETON_PLACEHOLDER_ROLE_COUNT }).map((_, cellIndex) => (
                                    <TableCell key={cellIndex} className='min-w-32.5 text-center'>
                                        <Skeleton className='mx-auto size-4 rounded-sm' />
                                    </TableCell>
                                ))}
                            </TableRow>
                        ))}
                    </TableBody>
                ) : (
                    groups.map(group => <PermissionGroupSection key={group.name} group={group} roles={roles} />)
                )}
            </Table>
        </div>
    );
};
