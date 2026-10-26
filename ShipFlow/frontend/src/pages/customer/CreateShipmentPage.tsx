import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { useCreateShipmentMutation } from '@/features/shipments/hooks';
import { usePageTitle } from '@/hooks/usePageTitle';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardFooter } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Input, Textarea } from '@/components/ui/Input';
import { FormField } from '@/components/ui/FormField';
import { useToast } from '@/components/ui/Toast';
import { AppError } from '@/api/types';
import { Plus, Trash2, MapPin, Package, Clock, ArrowRight, AlertTriangle, Sparkles } from 'lucide-react';

const createShipmentSchema = z.object({
  reference: z.string().max(50, 'Reference cannot exceed 50 characters').optional(),
  cargo: z.object({
    description: z.string().min(1, 'Cargo description is required').max(500, 'Max 500 characters'),
    weightKg: z.coerce.number().positive('Weight must be greater than 0 kg'),
    volumeM3: z.coerce.number().min(0, 'Volume cannot be negative'),
  }),
  stops: z
    .array(
      z.object({
        address: z.string().min(1, 'Address is required').max(300, 'Address cannot exceed 300 characters'),
        latitude: z.coerce.number().min(-90).max(90, 'Latitude must be between -90 and 90'),
        longitude: z.coerce.number().min(-180).max(180, 'Longitude must be between -180 and 180'),
        windowStartUtc: z.string().optional().nullable(),
        windowEndUtc: z.string().optional().nullable(),
      })
    )
    .min(2, 'A shipment requires at least 2 stops (pickup and delivery)')
    .max(20, 'Maximum 20 stops allowed')
    .refine(
      (stops) => {
        // Stop 0 (pickup) must have windowStart and windowEnd
        const pickup = stops[0];
        return !!pickup.windowStartUtc && !!pickup.windowEndUtc;
      },
      {
        message: 'The pickup stop must specify both a window start and end time',
        path: ['stops', 0, 'windowStartUtc'],
      }
    )
    .refine(
      (stops) => {
        const pickup = stops[0];
        if (pickup.windowStartUtc && pickup.windowEndUtc) {
          return new Date(pickup.windowEndUtc) > new Date(pickup.windowStartUtc);
        }
        return true;
      },
      {
        message: 'Pickup window end time must be after the start time',
        path: ['stops', 0, 'windowEndUtc'],
      }
    ),
});

type CreateShipmentFormData = z.infer<typeof createShipmentSchema>;

// Quick Egyptian logistics coordinate presets for rapid testing
const PRESETS = [
  { name: 'Cairo Dry Port', lat: 30.0444, lng: 31.2357, address: '6th of October City, Giza' },
  { name: 'Alexandria Sea Terminal', lat: 31.2001, lng: 29.9187, address: 'Alexandria Port Gate 4' },
  { name: 'Port Said Logistics Hub', lat: 31.2653, lng: 32.3019, address: 'East Port Said Bypass Zone' },
  { name: 'Suez Canal Terminal', lat: 29.9668, lng: 32.5498, address: 'Ain Sokhna Port Zone' },
];

export const CreateShipmentPage: React.FC = () => {
  usePageTitle('New Consignment');
  const navigate = useNavigate();
  const toast = useToast();
  const createMutation = useCreateShipmentMutation();

  const [serverError, setServerError] = useState<string | null>(null);

  // Default time windows: tomorrow from 09:00Z to 17:00Z
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const tomorrowStr = tomorrow.toISOString().split('T')[0];

  const defaultStart = `${tomorrowStr}T09:00:00Z`;
  const defaultEnd = `${tomorrowStr}T17:00:00Z`;

  const {
    register,
    control,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<CreateShipmentFormData>({
    resolver: zodResolver(createShipmentSchema),
    defaultValues: {
      reference: '',
      cargo: {
        description: '',
        weightKg: 1000,
        volumeM3: 10,
      },
      stops: [
        {
          address: 'Cairo Logistics Hub, 6th of October Industrial Zone',
          latitude: 30.0444,
          longitude: 31.2357,
          windowStartUtc: defaultStart,
          windowEndUtc: defaultEnd,
        },
        {
          address: 'Alexandria Sea Port, Terminal Gate 2',
          latitude: 31.2001,
          longitude: 29.9187,
          windowStartUtc: '',
          windowEndUtc: '',
        },
      ],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control,
    name: 'stops',
  });

  const onSubmit = async (data: CreateShipmentFormData) => {
    setServerError(null);

    // Format ISO UTC timestamps
    const formattedStops = data.stops.map((s) => ({
      address: s.address.trim(),
      latitude: Number(s.latitude),
      longitude: Number(s.longitude),
      windowStartUtc: s.windowStartUtc ? new Date(s.windowStartUtc).toISOString() : null,
      windowEndUtc: s.windowEndUtc ? new Date(s.windowEndUtc).toISOString() : null,
    }));

    try {
      const created = await createMutation.mutateAsync({
        reference: data.reference?.trim() || null,
        cargo: {
          description: data.cargo.description.trim(),
          weightKg: Number(data.cargo.weightKg),
          volumeM3: Number(data.cargo.volumeM3),
        },
        stops: formattedStops,
      });

      toast.success('Shipment Created', 'Consignment has been registered in Pending state.');
      navigate(`/app/shipments/${created.id}`);
    } catch (err: unknown) {
      const appErr = err as AppError;
      setServerError(appErr.message || 'Failed to create shipment. Please review inputs.');
    }
  };

  const applyPreset = (index: number, preset: (typeof PRESETS)[0]) => {
    setValue(`stops.${index}.address`, preset.address);
    setValue(`stops.${index}.latitude`, preset.lat);
    setValue(`stops.${index}.longitude`, preset.lng);
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <PageHeader
        title="Register New Shipment"
        description="Specify cargo dimensions and stop schedule. Pickup requires a valid UTC service window."
        breadcrumbs={[
          { label: 'Dashboard', href: '/app' },
          { label: 'Shipments', href: '/app/shipments' },
          { label: 'New' },
        ]}
      />

      {serverError && (
        <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-xs text-red-700 flex items-start gap-2.5">
          <AlertTriangle className="w-4 h-4 text-red-600 flex-shrink-0 mt-0.5" />
          <span>{serverError}</span>
        </div>
      )}

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        {/* Section 1: General & Cargo Specs */}
        <Card>
          <CardHeader>
            <div className="flex items-center gap-2">
              <Package className="w-5 h-5 text-brand" />
              <CardTitle>Cargo &amp; Consignment Overview</CardTitle>
            </div>
            <CardDescription>Weight and volume will be used by carriers to match vehicle capacity</CardDescription>
          </CardHeader>

          <CardContent className="space-y-4">
            <FormField
              label="Shipment Reference (Optional)"
              hint="Custom business order or reference ID"
              error={errors.reference?.message}
            >
              <Input placeholder="e.g. PO-98421-EGY" {...register('reference')} />
            </FormField>

            <FormField
              label="Cargo Description"
              required
              hint="Detailed description of goods (e.g. 'Dry electronics in pallet boxes')"
              error={errors.cargo?.description?.message}
            >
              <Textarea
                rows={2}
                placeholder="Describe package contents, fragile markings, or handling requirements..."
                {...register('cargo.description')}
              />
            </FormField>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <FormField
                label="Total Weight (kg)"
                required
                hint="Greater than 0 kg"
                error={errors.cargo?.weightKg?.message}
              >
                <Input type="number" step="0.1" min="0.1" {...register('cargo.weightKg')} />
              </FormField>

              <FormField
                label="Total Volume (m³)"
                required
                hint="Greater than or equal to 0 m³"
                error={errors.cargo?.volumeM3?.message}
              >
                <Input type="number" step="0.01" min="0" {...register('cargo.volumeM3')} />
              </FormField>
            </div>
          </CardContent>
        </Card>

        {/* Section 2: Stops & Routing Sequence */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center gap-2">
                <MapPin className="w-5 h-5 text-brand" />
                <div>
                  <CardTitle>Delivery Stops &amp; Schedule</CardTitle>
                  <CardDescription>Between 2 and 20 sequenced stops</CardDescription>
                </div>
              </div>

              {fields.length < 20 && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    append({
                      address: '',
                      latitude: 30.0444,
                      longitude: 31.2357,
                      windowStartUtc: '',
                      windowEndUtc: '',
                    })
                  }
                  leftIcon={<Plus className="w-4 h-4" />}
                >
                  Add Stop
                </Button>
              )}
            </div>
          </CardHeader>

          <CardContent className="space-y-6">
            {fields.map((field, idx) => {
              const isPickup = idx === 0;
              const isDelivery = idx === fields.length - 1;

              return (
                <div
                  key={field.id}
                  className="p-4 sm:p-5 rounded-2xl bg-neutral-appBg/60 border border-neutral-border space-y-4 relative"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span className="w-6 h-6 rounded-full bg-brand text-white font-bold text-xs flex items-center justify-center">
                        {idx + 1}
                      </span>
                      <span className="text-sm font-bold text-neutral-primary">
                        {isPickup ? 'Stop 1: Pickup Location (Required Window)' : isDelivery ? `Stop ${idx + 1}: Final Destination` : `Stop ${idx + 1}: Intermediate Waypoint`}
                      </span>
                    </div>

                    {!isPickup && !isDelivery && (
                      <button
                        type="button"
                        onClick={() => remove(idx)}
                        className="text-red-600 hover:text-red-700 p-1 text-xs font-semibold flex items-center gap-1"
                      >
                        <Trash2 className="w-3.5 h-3.5" /> Remove
                      </button>
                    )}
                  </div>

                  {/* Preset quick buttons */}
                  <div className="flex items-center gap-1.5 flex-wrap text-[11px] text-neutral-secondary">
                    <span className="flex items-center gap-1 font-semibold text-neutral-primary">
                      <Sparkles className="w-3 h-3 text-brand" /> Presets:
                    </span>
                    {PRESETS.map((p) => (
                      <button
                        key={p.name}
                        type="button"
                        onClick={() => applyPreset(idx, p)}
                        className="px-2 py-0.5 rounded bg-white border border-neutral-border hover:border-brand text-neutral-secondary hover:text-brand transition-colors"
                      >
                        {p.name}
                      </button>
                    ))}
                  </div>

                  <FormField
                    label="Street Address / Facility Name"
                    required
                    error={errors.stops?.[idx]?.address?.message}
                  >
                    <Input
                      placeholder="e.g. 15 Industrial Ave, Cairo Logistics Zone"
                      {...register(`stops.${idx}.address`)}
                    />
                  </FormField>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <FormField
                      label="Latitude (-90 to 90)"
                      required
                      error={errors.stops?.[idx]?.latitude?.message}
                    >
                      <Input
                        type="number"
                        step="0.000001"
                        placeholder="30.0444"
                        {...register(`stops.${idx}.latitude`)}
                      />
                    </FormField>

                    <FormField
                      label="Longitude (-180 to 180)"
                      required
                      error={errors.stops?.[idx]?.longitude?.message}
                    >
                      <Input
                        type="number"
                        step="0.000001"
                        placeholder="31.2357"
                        {...register(`stops.${idx}.longitude`)}
                      />
                    </FormField>
                  </div>

                  {/* Window Start / End */}
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-1">
                    <FormField
                      label={`Service Window Start (UTC)${isPickup ? ' *' : ''}`}
                      hint="Earliest driver arrival time"
                      error={errors.stops?.[idx]?.windowStartUtc?.message}
                    >
                      <Input
                        type="datetime-local"
                        {...register(`stops.${idx}.windowStartUtc`)}
                      />
                    </FormField>

                    <FormField
                      label={`Service Window End (UTC)${isPickup ? ' *' : ''}`}
                      hint="Latest acceptable pickup/arrival time"
                      error={errors.stops?.[idx]?.windowEndUtc?.message}
                    >
                      <Input
                        type="datetime-local"
                        {...register(`stops.${idx}.windowEndUtc`)}
                      />
                    </FormField>
                  </div>
                </div>
              );
            })}
          </CardContent>

          <CardFooter>
            <Button variant="outline" size="sm" asChild>
              <Link to="/app/shipments">Cancel</Link>
            </Button>
            <Button
              type="submit"
              variant="primary"
              size="md"
              isLoading={createMutation.isPending || isSubmitting}
              rightIcon={<ArrowRight className="w-4 h-4" />}
            >
              Submit &amp; Create Shipment
            </Button>
          </CardFooter>
        </Card>
      </form>
    </div>
  );
};
