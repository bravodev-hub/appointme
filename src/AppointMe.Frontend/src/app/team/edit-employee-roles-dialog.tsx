import { RoleCheckboxGroup } from './role-checkbox-group';
import { roleSchema } from './roles';
import { Role, TeamMemberDto } from '@/api/appointme.schemas.ts';
import { getGetTeamQueryKey, useUpdateRoles } from '@/api/appointme.ts';
import { usePermission } from '@/components/auth';
import { SubmitButton } from '@/components/form';
import { IModalProps } from '@/components/modal-dialog';
import { Button } from '@/components/ui';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from '@/components/ui/dialog';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import * as z from 'zod';

const formSchema = z.object({
    roles: z.array(roleSchema).nonempty('At least one role is required'),
});

type EditRolesFormValues = z.infer<typeof formSchema>;

interface EditEmployeeRolesDialogProps extends IModalProps<void> {
    employee: TeamMemberDto;
}

export const EditEmployeeRolesDialog = ({ resolve, visible, employee }: EditEmployeeRolesDialogProps) => {
    const queryClient = useQueryClient();
    const canManageOwners = usePermission('employees:manage_owners');
    const { mutateAsync: updateRoles } = useUpdateRoles({
        mutation: {
            onSuccess: async () => {
                await queryClient.invalidateQueries({ queryKey: getGetTeamQueryKey() });
            },
        },
    });

    const { control, formState, handleSubmit, setError } = useForm<EditRolesFormValues>({
        resolver: zodResolver(formSchema),
        defaultValues: {
            roles: employee.roles as Role[],
        },
    });

    const onSubmit = async (data: EditRolesFormValues) => {
        try {
            await updateRoles({ id: employee.id, data: { roles: data.roles } });
            toast.success(`Roles updated for ${employee.fullName ?? employee.email}.`);
            resolve();
        } catch {
            setError('root', {
                type: 'server',
                message: 'Something went wrong. Please try again.',
            });
        }
    };

    return (
        <Dialog open={visible} onOpenChange={open => !open && resolve()}>
            <DialogTrigger asChild>
                <button type='button' aria-hidden tabIndex={-1} className='hidden' />
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Edit roles</DialogTitle>
                    <DialogDescription>
                        Update roles for <span className='font-medium'>{employee.fullName ?? employee.email}</span>.
                    </DialogDescription>
                </DialogHeader>
                <form id='edit-roles-form' onSubmit={handleSubmit(onSubmit)}>
                    <FieldGroup>
                        <Controller
                            name='roles'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel>Roles</FieldLabel>
                                    <RoleCheckboxGroup
                                        value={field.value}
                                        onChange={field.onChange}
                                        isRoleDisabled={role =>
                                            (employee.lockedRoles ?? []).includes(role) ||
                                            (role === Role.Owner && !canManageOwners)
                                        }
                                    />
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />
                        {formState.errors.root && <FieldError errors={[formState.errors.root]} />}
                    </FieldGroup>
                </form>
                <DialogFooter>
                    <Button type='button' variant='outline' onClick={() => resolve()}>
                        Cancel
                    </Button>
                    <SubmitButton
                        form='edit-roles-form'
                        isSubmitting={formState.isSubmitting}
                        submittingLabel='Saving…'
                    >
                        Save roles
                    </SubmitButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};
