import { getGetCustomersQueryKey, useRegisterCustomer } from '@/api/appointme';
import { Gender } from '@/api/appointme.schemas.ts';
import { SubmitButton } from '@/components/form';
import { Button, Input } from '@/components/ui';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { Controller, useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';
import * as z from 'zod';

const formSchema = z.object({
    firstName: z.string().min(1, 'First name is required').max(100),
    lastName: z.string().max(100).optional(),
    email: z.email('Invalid email address').max(200, 'Email has to be 200 characters at max'),
    dateOfBirth: z.string().optional(),
    gender: z.enum([Gender.Male, Gender.Female, Gender.Other]).optional(),
});

type CreateCustomerFormValues = z.infer<typeof formSchema>;

export const CreateCustomer = () => {
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const { mutateAsync: registerCustomer } = useRegisterCustomer();

    const { control, formState, handleSubmit } = useForm<CreateCustomerFormValues>({
        resolver: zodResolver(formSchema),
        mode: 'onTouched',
        defaultValues: {
            firstName: '',
            lastName: '',
            email: '',
            dateOfBirth: '',
            gender: undefined,
        },
    });

    const onSubmit = async (data: CreateCustomerFormValues) => {
        await registerCustomer({
            data: {
                firstName: data.firstName,
                lastName: data.lastName || null,
                email: data.email || null,
                dateOfBirth: data.dateOfBirth || null,
                gender: data.gender ?? null,
            },
        });
        await queryClient.invalidateQueries({ queryKey: getGetCustomersQueryKey() });
        toast.success('Customer created successfully');
        navigate('/customers');
    };

    return (
        <div className='mx-auto w-full max-w-lg'>
            <div className='mb-6'>
                <h1 className='text-2xl font-semibold tracking-tight'>New customer</h1>
                <p className='text-muted-foreground mt-1 text-sm'>Add a new customer to your records.</p>
            </div>

            <form onSubmit={handleSubmit(onSubmit)}>
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
                                    <Input {...field} id='lastName' placeholder='Doe' autoComplete='family-name' />
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
                                    <Input {...field} id='dateOfBirth' type='date' />
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
                                            <SelectItem value='Male'>Male</SelectItem>
                                            <SelectItem value='Female'>Female</SelectItem>
                                            <SelectItem value='Other'>Other</SelectItem>
                                        </SelectContent>
                                    </Select>
                                    {fieldState.error && <FieldError errors={[fieldState.error]} />}
                                </Field>
                            )}
                        />
                    </div>

                    <div className='flex justify-end gap-3 pt-2'>
                        <Button type='button' size='sm' variant='outline' asChild>
                            <Link to='/customers'>Cancel</Link>
                        </Button>
                        <SubmitButton size='sm' isSubmitting={formState.isSubmitting} submittingLabel='Creating…'>
                            Create customer
                        </SubmitButton>
                    </div>
                </FieldGroup>
            </form>
        </div>
    );
};
