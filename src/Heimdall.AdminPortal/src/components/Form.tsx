import React from 'react';

/* -------------------------------------------------------------------------- */
/* Form                                                                       */
/* -------------------------------------------------------------------------- */

interface FormProps extends React.FormHTMLAttributes<HTMLFormElement> {
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  children: React.ReactNode;
}

/**
 * Wrapper for HTML form with consistent styling.
 */
export function Form({ onSubmit, children, className = '', ...rest }: FormProps) {
  return (
    <form
      onSubmit={onSubmit}
      className={`space-y-4 ${className}`}
      noValidate
      {...rest}
    >
      {children}
    </form>
  );
}

/* -------------------------------------------------------------------------- */
/* FormField                                                                   */
/* -------------------------------------------------------------------------- */

interface FormFieldProps {
  label: string;
  htmlFor: string;
  error?: string;
  required?: boolean;
  children: React.ReactNode;
}

/**
 * Labeled form field wrapper with error display.
 */
export function FormField({ label, htmlFor, error, required, children }: FormFieldProps) {
  return (
    <div>
      <label htmlFor={htmlFor} className="block text-sm font-medium text-gray-700">
        {label}
        {required && <span className="ml-0.5 text-red-500">*</span>}
      </label>
      <div className="mt-1">{children}</div>
      {error && (
        <p className="mt-1 text-sm text-red-600" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* FormInput                                                                    */
/* -------------------------------------------------------------------------- */

interface FormInputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  error?: boolean;
}

/**
 * Styled text input for use within FormField.
 */
export const FormInput = React.forwardRef<HTMLInputElement, FormInputProps>(
  ({ error, className = '', ...props }, ref) => (
    <input
      ref={ref}
      className={`block w-full rounded-md border px-3 py-2 text-sm shadow-sm transition-colors
        focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary
        ${error ? 'border-red-300' : 'border-gray-300'}
        ${className}`}
      {...props}
    />
  )
);

FormInput.displayName = 'FormInput';

/* -------------------------------------------------------------------------- */
/* FormTextArea                                                                 */
/* -------------------------------------------------------------------------- */

interface FormTextAreaProps extends React.TextareaHTMLAttributes<HTMLTextAreaElement> {
  error?: boolean;
}

/**
 * Styled textarea for use within FormField.
 */
export const FormTextArea = React.forwardRef<HTMLTextAreaElement, FormTextAreaProps>(
  ({ error, className = '', ...props }, ref) => (
    <textarea
      ref={ref}
      className={`block w-full rounded-md border px-3 py-2 text-sm shadow-sm transition-colors
        focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary
        ${error ? 'border-red-300' : 'border-gray-300'}
        ${className}`}
      rows={3}
      {...props}
    />
  )
);

FormTextArea.displayName = 'FormTextArea';

/* -------------------------------------------------------------------------- */
/* FormSelect                                                                   */
/* -------------------------------------------------------------------------- */

interface FormSelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  error?: boolean;
  options: { value: string; label: string }[];
  placeholder?: string;
}

/**
 * Styled select dropdown for use within FormField.
 */
export const FormSelect = React.forwardRef<HTMLSelectElement, FormSelectProps>(
  ({ error, options, placeholder, className = '', ...props }, ref) => (
    <select
      ref={ref}
      className={`block w-full rounded-md border px-3 py-2 text-sm shadow-sm transition-colors
        focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary
        ${error ? 'border-red-300' : 'border-gray-300'}
        ${className}`}
      {...props}
    >
      {placeholder && (
        <option value="" disabled>
          {placeholder}
        </option>
      )}
      {options.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  )
);

FormSelect.displayName = 'FormSelect';

/* -------------------------------------------------------------------------- */
/* FormActions                                                                  */
/* -------------------------------------------------------------------------- */

interface FormActionsProps {
  children: React.ReactNode;
}

/**
 * Right-aligned action buttons container for forms.
 */
export function FormActions({ children }: FormActionsProps) {
  return <div className="flex justify-end gap-3 pt-4">{children}</div>;
}
