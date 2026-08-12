import { Gender } from '@/api/appointme.schemas';
import { getGetCustomerQueryKey, getGetCustomersQueryKey, useGetCustomer, useUpdateCustomer } from '@/api/appointme.ts';
import { SubmitButton } from '@/components/form';
import { IModalProps } from '@/components/modal-dialog';
import { Button, Input } from '@/components/ui';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { toast } from 'sonner';
import * as z from 'zod';

const formSchema = z.object({
    firstName: z.string().min(1, 'First name is required').max(100),
    lastName: z.string().max(100).optional().nullable(),
    email: z
        .email('Invalid email address')
        .max(200, {
            error: ({ maximum }) => `Email has to be ${maximum} characters at max`,
        })
        .optional()
        .nullable(),
    dateOfBirth: z.string().optional().nullable(),
    gender: z.enum([Gender.Male, Gender.Female, Gender.Other]).optional().nullable(),
});

type EditCustomerFormValues = z.infer<typeof formSchema>;

interface EditCustomerDialogProps extends IModalProps<void> {
    customerId: string;
}

export const EditCustomerDialog = ({ resolve, visible, customerId }: EditCustomerDialogProps) => {
    const queryClient = useQueryClient();
    const { data: customer } = useGetCustomer(customerId);
    const { mutateAsync: updateCustomer } = useUpdateCustomer();

    const { control, formState, handleSubmit, reset } = useForm<EditCustomerFormValues>({
        resolver: zodResolver(formSchema),
        defaultValues: {
            firstName: '',
        },
    });

    useEffect(() => {
        if (!customer) {
            return;
        }
        reset({
            ...customer,
        });
    }, [customer, reset]);

    const onSubmit = async (data: EditCustomerFormValues) => {
        await updateCustomer({
            id: customerId,
            data,
        });
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: getGetCustomersQueryKey() }),
            queryClient.invalidateQueries({ queryKey: getGetCustomerQueryKey(customerId) }),
        ]);
        toast.success(`${customer?.fullName} has been updated.`);
        resolve();
    };

    return (
        <Dialog open={visible} onOpenChange={open => !open && resolve()}>
            <DialogTrigger asChild>
                <button type='button' aria-hidden tabIndex={-1} className='hidden' />
            </DialogTrigger>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>Edit customer</DialogTitle>
                    <DialogDescription>
                        Update details for <span className='font-medium'>{customer?.fullName}</span>.
                    </DialogDescription>
                </DialogHeader>
                <form id='edit-customer-form' onSubmit={handleSubmit(onSubmit)}>
                    <FieldGroup>
                        <div className='grid grid-cols-2 gap-4'>
                            <Controller
                                name='firstName'
                                control={control}
                                render={({ field, fieldState }) => (
                                    <Field data-invalid={fieldState.invalid}>
                                        <FieldLabel htmlFor='firstName'>
                                            First name <span className='text-destructive'>*</span>
                                        </FieldLabel>
                                        <Input {...field} id='firstName' placeholder='Jane' autoComplete='given-name' />
                                        {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                    </Field>
                                )}
                            />
                            <Controller
                                name='lastName'
                                control={control}
                                render={({ field, fieldState }) => (
                                    <Field data-invalid={fieldState.invalid}>
                                        <FieldLabel htmlFor='lastName'>Last name</FieldLabel>
                                        <Input
                                            {...field}
                                            value={field.value ?? ''}
                                            id='lastName'
                                            placeholder='Doe'
                                            autoComplete='family-name'
                                        />
                                        {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                    </Field>
                                )}
                            />
                        </div>

                        <Controller
                            name='email'
                            control={control}
                            render={({ field, fieldState }) => (
                                <Field data-invalid={fieldState.invalid}>
                                    <FieldLabel htmlFor='email'>Email</FieldLabel>
                                    <Input
                                        {...field}
                                        value={field.value ?? ''}
                                        id='email'
                                        type='email'
                                        placeholder='jane@example.com'
                                        autoComplete='email'
                                    />
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />

                        <div className='grid grid-cols-2 gap-4'>
                            <Controller
                                name='dateOfBirth'
                                control={control}
                                render={({ field, fieldState }) => (
                                    <Field data-invalid={fieldState.invalid}>
                                        <FieldLabel htmlFor='dateOfBirth'>Date of birth</FieldLabel>
                                        <Input {...field} value={field.value ?? ''} id='dateOfBirth' type='date' />
                                        {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                    </Field>
                                )}
                            />
                            <Controller
                                name='gender'
                                control={control}
                                render={({ field, fieldState }) => (
                                    <Field data-invalid={fieldState.invalid}>
                                        <FieldLabel htmlFor='gender'>Gender</FieldLabel>
                                        <Select name='gender' value={field.value ?? ''} onValueChange={field.onChange}>
                                            <SelectTrigger id='gender' className='w-full'>
                                                <SelectValue placeholder='Select gender' />
                                            </SelectTrigger>
                                            <SelectContent>
                                                <SelectItem value={Gender.Male}>Male</SelectItem>
                                                <SelectItem value={Gender.Female}>Female</SelectItem>
                                                <SelectItem value={Gender.Other}>Other</SelectItem>
                                            </SelectContent>
                                        </Select>
                                        {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                    </Field>
                                )}
                            />
                        </div>
                    </FieldGroup>
                </form>
                <DialogFooter>
                    <Button type='button' variant='outline' onClick={() => resolve()}>
                        Cancel
                    </Button>
                    <SubmitButton
                        form='edit-customer-form'
                        isSubmitting={formState.isSubmitting}
                        submittingLabel='Saving…'
                        disabled={!customer}
                    >
                        Save changes
                    </SubmitButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};
