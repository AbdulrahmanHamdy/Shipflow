import { describe, it, expect } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RegisterPage } from './RegisterPage';
import { renderWithProviders } from '@/test/test-utils';

describe('RegisterPage', () => {
  it('renders registration form explicitly stating Customer role assignment', () => {
    renderWithProviders(<RegisterPage />);

    expect(screen.getByRole('heading', { name: /create customer account/i })).toBeInTheDocument();
    expect(screen.getByText(/creates a customer account/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^password/i)).toBeInTheDocument();
  });

  it('validates password requirements before submitting', async () => {
    const user = userEvent.setup();
    renderWithProviders(<RegisterPage />);

    await user.type(screen.getByLabelText(/full name/i), 'Jane Customer');
    await user.type(screen.getByLabelText(/email address/i), 'jane@customer.local');
    await user.type(screen.getByLabelText(/^password/i), 'short');
    await user.type(screen.getByLabelText(/confirm password/i), 'mismatch');

    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(screen.getByText(/passwords do not match/i)).toBeInTheDocument();
    });
  });

  it('displays conflict error message when email is already registered', async () => {
    const user = userEvent.setup();
    renderWithProviders(<RegisterPage />);

    await user.type(screen.getByLabelText(/full name/i), 'Duplicate User');
    await user.type(screen.getByLabelText(/email address/i), 'duplicate@shipflow.local');
    await user.type(screen.getByLabelText(/^password/i), 'ValidPassword123!');
    await user.type(screen.getByLabelText(/confirm password/i), 'ValidPassword123!');

    await user.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(screen.getByText(/an account with this email address already exists/i)).toBeInTheDocument();
    });
  });
});
