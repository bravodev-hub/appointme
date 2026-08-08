import { useSignup } from '@/api/appointme.ts';
import { isAxiosError } from 'axios';
import G from '@/assets/g.svg?react';
import { Spinner } from '@/components/ui';
import { Button } from '@/components/ui/button';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel, FieldSeparator } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { zodResolver } from '@hookform/resolvers/zod';
import { ComponentProps } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useNavigate } from 'react-router';
import * as z from 'zod';

const formSchema = z.object({
    name: z.string().trim().min(1, 'Name is required').max(250, 'Name is too long'),
    email: z.email('Invalid email address').max(200, 'Email has to be 200 characters at max'),
});

type SignupFormValues = z.infer<typeof formSchema>;

export const SignupForm = ({ className, ...props }: ComponentProps<'form'>) => {
    const navigate = useNavigate();
    const { mutateAsync: signup } = useSignup();
    const { control, formState, handleSubmit, setError } = useForm<SignupFormValues>({
        resolver: zodResolver(formSchema),
        mode: 'onTouched',
        defaultValues: {
            name: '',
            email: '',
        },
    });

    const onSubmit = async (data: SignupFormValues) => {
        try {
            await signup({ data });
            navigate(`/auth/verify-email/${data.email}`);
        } catch (error) {
            if (isAxiosError(error)) {
                const code = error.response?.data?.code;
                const status = error.response?.status;

                if (status === 409 && code === 'email_already_exists') {
                    setError(
                        'email',
                        { type: 'server', message: 'This email is already registered' },
                        { shouldFocus: true },
                    );
                    return;
                }

                if (status === 400 && code === 'invalid_email_for_idp') {
                    setError(
                        'email',
                        {
                            type: 'server',
                            message:
                                'This email format isn’t supported (e.g. “+” aliases). Please use a different email.',
                        },
                        { shouldFocus: true },
                    );
                    return;
                }
            }

            setError('root', {
                type: 'server',
                message: 'Something went wrong. Please try again.',
            });
        }
    };

    return (
        <form className={cn('flex flex-col gap-6', className)} onSubmit={handleSubmit(onSubmit)} {...props}>
            <div className='flex flex-col items-center gap-1 text-center'>
                <h1 className='text-2xl font-bold'>Create your account</h1>
                <p className='text-muted-foreground text-sm text-balance'>We’ll email you a link to finish setup</p>
            </div>
            <FieldGroup>
                <Field>
                    <Button
                        variant='outline'
                        type='button'
                        onClick={() => {
                            window.location.href = '/auth/login?provider=google&returnUrl=/onboarding';
                        }}
                    >
                        <G />
                        Continue with Google
                    </Button>
                </Field>

                <FieldSeparator>Or continue with email</FieldSeparator>

                <Controller
                    name='name'
                    control={control}
                    render={({ field, fieldState }) => (
                        <Field data-invalid={fieldState.invalid}>
                            <FieldLabel htmlFor='name'>Full Name</FieldLabel>
                            <Input
                                {...field}
                                id='name'
                                type='text'
                                placeholder='John Doe'
                                autoComplete='name'
                                aria-invalid={fieldState.invalid}
                                aria-describedby={fieldState.invalid ? 'name-error' : undefined}
                            />
                            {fieldState.invalid && <FieldError id='name-error' errors={[fieldState.error]} />}
                        </Field>
                    )}
                />
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
                                placeholder='m@example.com'
                                autoComplete='email'
                                aria-invalid={fieldState.invalid}
                                aria-describedby={fieldState.invalid ? 'email-error' : undefined}
                            />
                            {fieldState.error && <FieldError id='email-error' errors={[fieldState.error]} />}
                        </Field>
                    )}
                />
                {formState.errors.root && <FieldError errors={[formState.errors.root]} />}
                <Field>
                    <Button type='submit' disabled={formState.isSubmitting}>
                        {formState.isSubmitting ? (
                            <>
                                <Spinner />
                                Creating account…
                            </>
                        ) : (
                            'Create Account'
                        )}
                    </Button>
                    <FieldDescription className='px-6 text-center'>
                        Already have an account? <a href='/auth/login'>Sign in</a>
                    </FieldDescription>
                </Field>
            </FieldGroup>
        </form>
    );
};
