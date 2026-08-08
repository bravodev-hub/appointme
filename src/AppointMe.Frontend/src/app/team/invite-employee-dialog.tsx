import { getGetTeamQueryKey, useInviteEmployee } from '@/api/appointme.ts';
import { isAxiosError } from 'axios';
import { RoleCheckboxGroup } from './role-checkbox-group';
import { roleSchema } from './roles';
import { Role } from '@/api/appointme.schemas.ts';
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
import { Input } from '@/components/ui/input';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import * as z from 'zod';

const formSchema = z.object({
    email: z.email('Invalid email address').max(200, {
        error: check => `Email has to be ${check.maximum} characters at max`,
    }),
    roles: z.array(roleSchema).nonempty('At least one role is required'),
});

type InviteFormValues = z.infer<typeof formSchema>;

export const InviteEmployeeDialog = ({ resolve, visible }: IModalProps<void>) => {
    const queryClient = useQueryClient();
    const canManageOwners = usePermission('employees:manage_owners');
    const { mutateAsync: inviteEmployee } = useInviteEmployee({
        mutation: {
            onSuccess: async () => {
                await queryClient.invalidateQueries({ queryKey: getGetTeamQueryKey() });
            },
        },
    });
    const { control, formState, handleSubmit, setError, reset } = useForm<InviteFormValues>({
        resolver: zodResolver(formSchema),
        mode: 'onTouched',
        defaultValues: {
            email: '',
            roles: [],
        },
    });

    const onSubmit = async (data: InviteFormValues) => {
        try {
            await inviteEmployee({ data: { email: data.email, roles: data.roles } });
            toast.success('Invitation sent successfully');
            reset();
            resolve();
        } catch (error) {
            if (isAxiosError(error)) {
                const code = error.response?.data?.code;
                switch (error.response?.status) {
                    case 409:
                        if (code === 'employee_already_exists') {
                            setError('email', {
                                type: 'server',
                                message: 'An employee with this email already exists',
                            });
                            return;
                        }
                        if (code === 'invitation_already_exists') {
                            setError('email', {
                                type: 'server',
                                message: 'A pending invitation already exists for this email',
                            });
                            return;
                        }
                        break;
                }
            }

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
                    <DialogTitle>Invite employee</DialogTitle>
                    <DialogDescription>
                        Send an invitation to join your company. They will receive an email to set up their account.
                    </DialogDescription>
                </DialogHeader>
                <form id='invite-employee-form' onSubmit={handleSubmit(onSubmit)}>
                    <FieldGroup>
                        <Controller
                            name='email'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='invite-email'>Email</FieldLabel>
                                    <Input
                                        {...field}
                                        id='invite-email'
                                        type='email'
                                        placeholder='employee@example.com'
                                        autoComplete='email'
                                    />
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />
                        <Controller
                            name='roles'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel>Roles</FieldLabel>
                                    <RoleCheckboxGroup
                                        value={field.value}
                                        onChange={field.onChange}
                                        isRoleDisabled={role => role === Role.Owner && !canManageOwners}
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
                        form='invite-employee-form'
                        isSubmitting={formState.isSubmitting}
                        submittingLabel='Sending…'
                    >
                        Send invitation
                    </SubmitButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};
