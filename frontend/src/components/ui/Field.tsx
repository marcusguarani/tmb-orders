import { forwardRef, useId, type InputHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  hint?: string
}

/** Campo de formulário com label, mensagem de erro e acessibilidade (aria-*). */
export const Field = forwardRef<HTMLInputElement, FieldProps>(({ label, error, hint, className, id, required, ...props }, ref) => {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const messageId = `${inputId}-message`

  return (
    <div className="space-y-1.5">
      <label htmlFor={inputId} className="block text-sm font-medium text-zinc-700 dark:text-zinc-300">
        {label}
        {required && (
          <span className="ml-0.5 text-red-500" aria-hidden>
            *
          </span>
        )}
      </label>
      <input
        ref={ref}
        id={inputId}
        aria-invalid={error ? true : undefined}
        aria-required={required || undefined}
        aria-describedby={error || hint ? messageId : undefined}
        className={cn(
          'block h-10 w-full rounded-lg border-0 bg-white px-3 text-sm text-zinc-900 shadow-sm ring-1 ring-inset ring-zinc-300 placeholder:text-zinc-400',
          'focus:outline-none focus:ring-2 focus:ring-indigo-500',
          'dark:bg-zinc-950 dark:text-zinc-100 dark:ring-zinc-700 dark:placeholder:text-zinc-500',
          error && 'ring-red-400 focus:ring-red-500 dark:ring-red-500/60',
          className,
        )}
        {...props}
      />
      {(error || hint) && (
        <p id={messageId} className={cn('text-xs', error ? 'text-red-600 dark:text-red-400' : 'text-zinc-500')}>
          {error ?? hint}
        </p>
      )}
    </div>
  )
})
Field.displayName = 'Field'
