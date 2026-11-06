import React, { useState, useRef } from 'react';
import {
  Sparkles,
  Camera,
  Upload,
  X,
  Plus,
  Trash2,
  Edit2,
  CheckCircle2,
  AlertCircle,
  Truck,
  Box,
  Wrench,
  ShieldAlert,
  ChevronRight,
  Info,
  Loader2,
  RefreshCw,
  Sliders,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/Card';
import { Input, Textarea } from '@/components/ui/Input';
import { FormField } from '@/components/ui/FormField';
import { Modal } from '@/components/ui/Modal';
import { useToast } from '@/components/ui/Toast';
import {
  CargoCategory,
  CargoItemDto,
  PackingComplexity,
  FragilityLevel,
  UpdateCargoItemInputDto,
  VehicleRecommendationDto,
  CargoAnalysisResponseDto,
} from '@/api/types';
import {
  useAnalyzeCargoMutation,
  useUpdateCargoItemsMutation,
  useConfirmCargoMutation,
  useRecommendVehiclesMutation,
} from '@/features/cargo/hooks';
import { cn } from '@/lib/utils';

interface SmartCargoEstimatorProps {
  onApplyCargo: (data: {
    description: string;
    weightKg: number;
    volumeM3: number;
    recommendedVehicle?: VehicleRecommendationDto;
  }) => void;
  initialValues?: {
    description?: string;
    weightKg?: number;
    volumeM3?: number;
  };
}

const CATEGORIES: CargoCategory[] = [
  'Bed',
  'Mattress',
  'Wardrobe',
  'Sofa',
  'Chair',
  'Table',
  'Desk',
  'Refrigerator',
  'WashingMachine',
  'Television',
  'Cabinet',
  'Box',
  'Appliance',
  'Electronics',
  'Other',
];

export const SmartCargoEstimator: React.FC<SmartCargoEstimatorProps> = ({
  onApplyCargo,
}) => {
  const toast = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const cameraInputRef = useRef<HTMLInputElement>(null);

  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [filePreviews, setFilePreviews] = useState<string[]>([]);
  const [analysisData, setAnalysisData] = useState<CargoAnalysisResponseDto | null>(null);
  const [isEditingItem, setIsEditingItem] = useState<boolean>(false);
  const [editingItemIndex, setEditingItemIndex] = useState<number | null>(null);
  const [editingItemForm, setEditingItemForm] = useState<UpdateCargoItemInputDto | null>(null);
  const [selectedVehicle, setSelectedVehicle] = useState<VehicleRecommendationDto | null>(null);
  const [isConfirmed, setIsConfirmed] = useState<boolean>(false);

  const analyzeMutation = useAnalyzeCargoMutation();
  const updateMutation = useUpdateCargoItemsMutation();
  const confirmMutation = useConfirmCargoMutation();
  const recommendMutation = useRecommendVehiclesMutation();

  const handleFilesSelected = (files: FileList | null) => {
    if (!files || files.length === 0) return;

    const newFiles = Array.from(files).filter((file) => {
      const valid = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'].includes(file.type);
      if (!valid) {
        toast.error('Invalid format', `${file.name} is not a supported format (JPEG, PNG, WebP)`);
        return false;
      }
      if (file.size > 10 * 1024 * 1024) {
        toast.error('File too large', `${file.name} exceeds 10MB limit`);
        return false;
      }
      return true;
    });

    const combined = [...selectedFiles, ...newFiles].slice(0, 10);
    setSelectedFiles(combined);

    // Generate previews
    const previews = combined.map((f) => URL.createObjectURL(f));
    setFilePreviews(previews);
  };

  const removeFile = (index: number) => {
    const updatedFiles = selectedFiles.filter((_, i) => i !== index);
    setSelectedFiles(updatedFiles);
    const updatedPreviews = filePreviews.filter((_, i) => i !== index);
    setFilePreviews(updatedPreviews);
  };

  const handleAnalyze = async () => {
    if (selectedFiles.length === 0) {
      toast.error('Photos Required', 'Please upload or capture at least one photo of your cargo.');
      return;
    }

    try {
      const result = await analyzeMutation.mutateAsync(selectedFiles);
      setAnalysisData(result);
      if (result.vehicleRecommendations?.bestMatch) {
        setSelectedVehicle(result.vehicleRecommendations.bestMatch);
      }
      toast.success(
        'AI Estimation Complete',
        `Identified ${result.items.length} items with realistic dimensions and disassembly estimates.`
      );
    } catch (err: any) {
      toast.error('Analysis Failed', err.message || 'Failed to analyze photos with Gemini Vision.');
    }
  };

  const openEditModal = (index: number) => {
    if (!analysisData) return;
    const item = analysisData.items[index];
    setEditingItemIndex(index);
    setEditingItemForm({
      id: item.id,
      name: item.name,
      category: item.category,
      quantity: item.quantity,
      lengthCm: item.lengthCm,
      widthCm: item.widthCm,
      heightCm: item.heightCm,
      weightKg: item.weightKg,
      canBeDisassembled: item.canBeDisassembled,
      disassemblyNotes: item.disassemblyNotes || '',
      packedLengthCm: item.packedLengthCm,
      packedWidthCm: item.packedWidthCm,
      packedHeightCm: item.packedHeightCm,
      packedWeightKg: item.packedWeightKg,
      packingComplexity: item.packingComplexity,
      fragility: item.fragility,
      specialHandling: item.specialHandling || '',
      confidenceScore: item.confidenceScore,
      estimationNotes: item.estimationNotes || '',
    });
    setIsEditingItem(true);
  };

  const openAddNewModal = () => {
    setEditingItemIndex(null);
    setEditingItemForm({
      name: '',
      category: 'Other',
      quantity: 1,
      lengthCm: 100,
      widthCm: 80,
      heightCm: 75,
      weightKg: 25,
      canBeDisassembled: false,
      disassemblyNotes: '',
      packedLengthCm: 100,
      packedWidthCm: 80,
      packedHeightCm: 75,
      packedWeightKg: 25,
      packingComplexity: 'Low',
      fragility: 'Low',
      specialHandling: '',
      confidenceScore: 1.0,
      estimationNotes: 'Manually added item',
    });
    setIsEditingItem(true);
  };

  const saveItemChanges = async () => {
    if (!editingItemForm || !analysisData) return;

    if (!editingItemForm.name.trim()) {
      toast.error('Name required', 'Please provide a name for the item.');
      return;
    }

    let updatedItemsList: UpdateCargoItemInputDto[];
    if (editingItemIndex !== null) {
      updatedItemsList = analysisData.items.map((it, idx) =>
        idx === editingItemIndex
          ? editingItemForm
          : {
              id: it.id,
              name: it.name,
              category: it.category,
              quantity: it.quantity,
              lengthCm: it.lengthCm,
              widthCm: it.widthCm,
              heightCm: it.heightCm,
              weightKg: it.weightKg,
              canBeDisassembled: it.canBeDisassembled,
              disassemblyNotes: it.disassemblyNotes,
              packedLengthCm: it.packedLengthCm,
              packedWidthCm: it.packedWidthCm,
              packedHeightCm: it.packedHeightCm,
              packedWeightKg: it.packedWeightKg,
              packingComplexity: it.packingComplexity,
              fragility: it.fragility,
              specialHandling: it.specialHandling,
              confidenceScore: it.confidenceScore,
              estimationNotes: it.estimationNotes,
            }
      );
    } else {
      const existing = analysisData.items.map((it) => ({
        id: it.id,
        name: it.name,
        category: it.category,
        quantity: it.quantity,
        lengthCm: it.lengthCm,
        widthCm: it.widthCm,
        heightCm: it.heightCm,
        weightKg: it.weightKg,
        canBeDisassembled: it.canBeDisassembled,
        disassemblyNotes: it.disassemblyNotes,
        packedLengthCm: it.packedLengthCm,
        packedWidthCm: it.packedWidthCm,
        packedHeightCm: it.packedHeightCm,
        packedWeightKg: it.packedWeightKg,
        packingComplexity: it.packingComplexity,
        fragility: it.fragility,
        specialHandling: it.specialHandling,
        confidenceScore: it.confidenceScore,
        estimationNotes: it.estimationNotes,
      }));
      updatedItemsList = [...existing, editingItemForm];
    }

    try {
      const updated = await updateMutation.mutateAsync({
        id: analysisData.sessionId,
        items: updatedItemsList,
      });
      setAnalysisData(updated);
      if (updated.vehicleRecommendations?.bestMatch) {
        setSelectedVehicle(updated.vehicleRecommendations.bestMatch);
      }
      setIsEditingItem(false);
      toast.success('Inventory Updated', 'Recalculated weight, volume, and vehicle recommendations.');
    } catch (err: any) {
      toast.error('Update Failed', err.message || 'Could not update inventory.');
    }
  };

  const removeItem = async (index: number) => {
    if (!analysisData) return;
    const remaining = analysisData.items
      .filter((_, idx) => idx !== index)
      .map((it) => ({
        id: it.id,
        name: it.name,
        category: it.category,
        quantity: it.quantity,
        lengthCm: it.lengthCm,
        widthCm: it.widthCm,
        heightCm: it.heightCm,
        weightKg: it.weightKg,
        canBeDisassembled: it.canBeDisassembled,
        disassemblyNotes: it.disassemblyNotes,
        packedLengthCm: it.packedLengthCm,
        packedWidthCm: it.packedWidthCm,
        packedHeightCm: it.packedHeightCm,
        packedWeightKg: it.packedWeightKg,
        packingComplexity: it.packingComplexity,
        fragility: it.fragility,
        specialHandling: it.specialHandling,
        confidenceScore: it.confidenceScore,
        estimationNotes: it.estimationNotes,
      }));

    if (remaining.length === 0) {
      toast.error('Minimum Items', 'Cargo must contain at least one item.');
      return;
    }

    try {
      const updated = await updateMutation.mutateAsync({
        id: analysisData.sessionId,
        items: remaining,
      });
      setAnalysisData(updated);
      toast.success('Item Removed', 'Recalculated cargo requirements.');
    } catch (err: any) {
      toast.error('Failed to remove item', err.message);
    }
  };

  const handleConfirmAndApply = async () => {
    if (!analysisData) return;

    try {
      await confirmMutation.mutateAsync(analysisData.sessionId);
      setIsConfirmed(true);

      // Build structured cargo summary description
      const itemSummaries = analysisData.items.map((i) => {
        const disText = i.canBeDisassembled ? ' (Disassembled)' : '';
        return `${i.quantity}x ${i.name}${disText}`;
      });

      const specialNotes = analysisData.calculation.specialHandlingRequirements;
      let fullDescription = itemSummaries.join(', ');
      if (specialNotes && specialNotes.length > 0) {
        fullDescription += ` | Special handling: ${specialNotes.slice(0, 2).join('; ')}`;
      }

      onApplyCargo({
        description: fullDescription.slice(0, 500),
        weightKg: analysisData.calculation.totalWeightKg,
        volumeM3: analysisData.calculation.totalPackedVolumeM3,
        recommendedVehicle: selectedVehicle || analysisData.vehicleRecommendations?.bestMatch || undefined,
      });

      toast.success(
        'Cargo Inventory Applied',
        'Updated shipment weight, packed volume, description, and vehicle specs.'
      );
    } catch (err: any) {
      toast.error('Confirmation Failed', err.message);
    }
  };

  return (
    <div className="space-y-5">
      {/* AI Assistant Banner */}
      <div className="rounded-2xl p-5 bg-gradient-to-r from-emerald-950 via-forest-deep to-forest-night text-white border border-emerald-500/30 shadow-lg relative overflow-hidden">
        <div className="absolute top-0 right-0 translate-x-8 -translate-y-8 w-44 h-44 bg-emerald-500/10 rounded-full blur-2xl pointer-events-none" />

        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-emerald-500/20 border border-emerald-400/40 flex items-center justify-center text-emerald-300">
              <Sparkles className="w-5 h-5 animate-pulse" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-white tracking-tight">
                  ShipFlow AI — Smart Cargo Estimator
                </h3>
                <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 uppercase tracking-wider">
                  Gemini Vision
                </span>
              </div>
              <p className="text-xs text-neutral-300 mt-0.5">
                Upload photos of your furniture and cargo. AI estimates dimensions, evaluates disassembly, and matches suitable vehicles.
              </p>
            </div>
          </div>

          {analysisData && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setAnalysisData(null);
                setSelectedFiles([]);
                setFilePreviews([]);
                setIsConfirmed(false);
              }}
              leftIcon={<RefreshCw className="w-3.5 h-3.5" />}
              className="text-xs bg-white/10 hover:bg-white/20 text-white border-white/20"
            >
              Analyze New Photos
            </Button>
          )}
        </div>

        {/* Step 1: Photo Upload Section (if no analysis done yet) */}
        {!analysisData && (
          <div className="mt-5 space-y-4 pt-4 border-t border-white/10">
            {/* Hidden file inputs */}
            <input
              type="file"
              ref={fileInputRef}
              onChange={(e) => handleFilesSelected(e.target.files)}
              accept="image/jpeg,image/png,image/webp"
              multiple
              className="hidden"
            />
            <input
              type="file"
              ref={cameraInputRef}
              onChange={(e) => handleFilesSelected(e.target.files)}
              accept="image/*"
              capture="environment"
              className="hidden"
            />

            {/* Upload Area */}
            <div
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault();
                handleFilesSelected(e.dataTransfer.files);
              }}
              className="border-2 border-dashed border-emerald-500/40 hover:border-emerald-400 rounded-xl p-6 text-center bg-black/20 hover:bg-black/30 transition-all cursor-pointer"
              onClick={() => fileInputRef.current?.click()}
            >
              <div className="flex flex-col items-center gap-2">
                <div className="w-12 h-12 rounded-full bg-emerald-500/10 flex items-center justify-center text-emerald-400">
                  <Upload className="w-6 h-6" />
                </div>
                <div>
                  <p className="text-sm font-semibold text-white">
                    Drop furniture photos here, or browse files
                  </p>
                  <p className="text-xs text-neutral-400 mt-0.5">
                    Supports JPEG, PNG, WebP (up to 10 photos, max 10MB each)
                  </p>
                </div>
                <div className="flex items-center gap-2 mt-2" onClick={(e) => e.stopPropagation()}>
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={() => fileInputRef.current?.click()}
                    leftIcon={<Upload className="w-3.5 h-3.5" />}
                  >
                    Select Photos
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => cameraInputRef.current?.click()}
                    leftIcon={<Camera className="w-3.5 h-3.5" />}
                    className="bg-white/10 text-white border-white/20 hover:bg-white/20"
                  >
                    Take Photo
                  </Button>
                </div>
              </div>
            </div>

            {/* Thumbnail previews */}
            {filePreviews.length > 0 && (
              <div className="space-y-3">
                <div className="flex items-center justify-between text-xs text-neutral-300">
                  <span>
                    Selected Photos ({filePreviews.length}/10)
                  </span>
                  <span className="text-[11px] text-emerald-400 font-medium">
                    Tip: Photograph items from front and side angles for higher accuracy
                  </span>
                </div>

                <div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
                  {filePreviews.map((preview, idx) => (
                    <div
                      key={idx}
                      className="group relative rounded-xl overflow-hidden aspect-video bg-black/40 border border-white/20 shadow-sm"
                    >
                      <img
                        src={preview}
                        alt={`Upload ${idx + 1}`}
                        className="w-full h-full object-cover"
                      />
                      <button
                        type="button"
                        onClick={() => removeFile(idx)}
                        className="absolute top-1.5 right-1.5 w-6 h-6 rounded-full bg-black/70 text-white hover:bg-red-600 flex items-center justify-center transition-colors"
                        title="Remove photo"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  ))}
                </div>

                <div className="pt-2 flex justify-end">
                  <Button
                    type="button"
                    variant="primary"
                    size="md"
                    isLoading={analyzeMutation.isPending}
                    onClick={handleAnalyze}
                    leftIcon={<Sparkles className="w-4 h-4 text-emerald-300" />}
                    className="bg-emerald-600 hover:bg-emerald-500 text-white shadow-lg"
                  >
                    {analyzeMutation.isPending
                      ? 'Analyzing with Gemini Vision...'
                      : `Analyze ${filePreviews.length} Photo${filePreviews.length > 1 ? 's' : ''}`}
                  </Button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Loading Step Progress */}
      {analyzeMutation.isPending && (
        <Card className="border-emerald-200 bg-emerald-50/50 animate-pulse">
          <CardContent className="p-6 flex flex-col items-center justify-center text-center space-y-3">
            <Loader2 className="w-8 h-8 text-emerald-600 animate-spin" />
            <div>
              <p className="text-sm font-bold text-neutral-primary">
                Gemini Vision AI is analyzing your cargo photos...
              </p>
              <p className="text-xs text-neutral-secondary mt-1 max-w-md">
                Detecting furniture items, calculating 3D bounding geometry, identifying disassembly opportunities, and calculating packing volume.
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Step 2: Analysis Results View */}
      {analysisData && (
        <div className="space-y-6">
          {/* Key Metrics Bar */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div className="p-3.5 rounded-xl bg-white border border-neutral-border shadow-xs">
              <span className="text-[11px] font-semibold text-neutral-secondary uppercase tracking-wider block">
                Total Items
              </span>
              <div className="flex items-baseline gap-1.5 mt-1">
                <span className="text-xl font-extrabold text-neutral-primary">
                  {analysisData.calculation.totalItemsCount}
                </span>
                <span className="text-xs text-neutral-secondary">units</span>
              </div>
            </div>

            <div className="p-3.5 rounded-xl bg-white border border-neutral-border shadow-xs">
              <span className="text-[11px] font-semibold text-neutral-secondary uppercase tracking-wider block">
                Total Weight
              </span>
              <div className="flex items-baseline gap-1.5 mt-1">
                <span className="text-xl font-extrabold text-emerald-700">
                  {analysisData.calculation.totalWeightKg.toLocaleString()}
                </span>
                <span className="text-xs text-neutral-secondary">kg</span>
              </div>
            </div>

            <div className="p-3.5 rounded-xl bg-white border border-neutral-border shadow-xs">
              <span className="text-[11px] font-semibold text-neutral-secondary uppercase tracking-wider block">
                Packed Volume
              </span>
              <div className="flex items-baseline gap-1.5 mt-1">
                <span className="text-xl font-extrabold text-brand">
                  {analysisData.calculation.totalPackedVolumeM3}
                </span>
                <span className="text-xs text-neutral-secondary">m³</span>
              </div>
            </div>

            <div className="p-3.5 rounded-xl bg-white border border-neutral-border shadow-xs">
              <span className="text-[11px] font-semibold text-neutral-secondary uppercase tracking-wider block">
                Disassembly Savings
              </span>
              <div className="flex items-baseline gap-1.5 mt-1">
                <span className="text-xl font-extrabold text-emerald-600">
                  {analysisData.calculation.volumeSavingsPercentage}%
                </span>
                <span className="text-xs text-neutral-secondary">space saved</span>
              </div>
            </div>
          </div>

          {/* Items Inventory Table */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between w-full">
                <div>
                  <div className="flex items-center gap-2">
                    <Box className="w-5 h-5 text-brand" />
                    <CardTitle>Detected Cargo Inventory</CardTitle>
                  </div>
                  <CardDescription>
                    Review detected items. Click Edit to adjust dimensions, weight, or disassembly options.
                  </CardDescription>
                </div>

                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={openAddNewModal}
                  leftIcon={<Plus className="w-3.5 h-3.5" />}
                >
                  Add Item
                </Button>
              </div>
            </CardHeader>

            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse text-xs">
                  <thead>
                    <tr className="border-y border-neutral-border bg-neutral-appBg/80 text-neutral-secondary font-semibold">
                      <th className="py-2.5 px-4">Item &amp; Category</th>
                      <th className="py-2.5 px-3">Qty</th>
                      <th className="py-2.5 px-3">Original (L×W×H)</th>
                      <th className="py-2.5 px-3">Disassembly &amp; Packed</th>
                      <th className="py-2.5 px-3">Weight</th>
                      <th className="py-2.5 px-3">Handling</th>
                      <th className="py-2.5 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-neutral-border/60">
                    {analysisData.items.map((item, idx) => (
                      <tr key={item.id || idx} className="hover:bg-neutral-hover/50 transition-colors">
                        <td className="py-3 px-4">
                          <div className="font-bold text-neutral-primary text-sm">
                            {item.name}
                          </div>
                          <div className="flex items-center gap-1.5 mt-0.5">
                            <span className="inline-block px-1.5 py-0.5 rounded text-[10px] font-medium bg-neutral-100 text-neutral-700 border border-neutral-200">
                              {item.category}
                            </span>
                            {item.fragility !== 'Low' && (
                              <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded text-[10px] font-semibold bg-amber-50 text-amber-800 border border-amber-200">
                                <ShieldAlert className="w-2.5 h-2.5 text-amber-600" />
                                {item.fragility} Fragility
                              </span>
                            )}
                          </div>
                          {item.estimationNotes && (
                            <p className="text-[11px] text-neutral-secondary mt-1 italic">
                              "{item.estimationNotes}"
                            </p>
                          )}
                        </td>

                        <td className="py-3 px-3 font-semibold text-neutral-primary">
                          {item.quantity}×
                        </td>

                        <td className="py-3 px-3 text-neutral-secondary whitespace-nowrap">
                          <div>
                            {item.lengthCm} × {item.widthCm} × {item.heightCm} cm
                          </div>
                          <div className="text-[11px] text-neutral-muted">
                            {item.originalVolumeM3} m³
                          </div>
                        </td>

                        <td className="py-3 px-3 whitespace-nowrap">
                          {item.canBeDisassembled ? (
                            <div>
                              <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                                <Wrench className="w-3 h-3 text-emerald-600" />
                                Disassemblable
                              </span>
                              <div className="text-[11px] text-neutral-primary font-medium mt-1">
                                Packed: {item.packedLengthCm} × {item.packedWidthCm} × {item.packedHeightCm} cm
                              </div>
                              <div className="text-[10px] text-emerald-600 font-semibold">
                                {item.packedVolumeM3} m³ (saved {Math.max(0, Math.round((1 - item.packedVolumeM3 / item.originalVolumeM3) * 100))}% vol)
                              </div>
                            </div>
                          ) : (
                            <div>
                              <span className="text-[11px] text-neutral-secondary font-medium">
                                Shipped whole
                              </span>
                              <div className="text-[11px] text-neutral-muted">
                                {item.packedVolumeM3} m³
                              </div>
                            </div>
                          )}
                        </td>

                        <td className="py-3 px-3 whitespace-nowrap font-medium text-neutral-primary">
                          {item.packedWeightKg} kg
                        </td>

                        <td className="py-3 px-3 max-w-[160px]">
                          {item.specialHandling ? (
                            <span className="text-[11px] text-neutral-secondary truncate block" title={item.specialHandling}>
                              {item.specialHandling}
                            </span>
                          ) : (
                            <span className="text-[11px] text-neutral-muted">—</span>
                          )}
                        </td>

                        <td className="py-3 px-3 text-right whitespace-nowrap">
                          <div className="flex items-center justify-end gap-1">
                            <button
                              type="button"
                              onClick={() => openEditModal(idx)}
                              className="p-1.5 rounded-lg text-neutral-secondary hover:text-brand hover:bg-neutral-hover transition-colors"
                              title="Edit item"
                            >
                              <Edit2 className="w-3.5 h-3.5" />
                            </button>
                            <button
                              type="button"
                              onClick={() => removeItem(idx)}
                              className="p-1.5 rounded-lg text-neutral-secondary hover:text-red-600 hover:bg-red-50 transition-colors"
                              title="Delete item"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>

          {/* Step 3: Smart Vehicle Recommendations */}
          {analysisData.vehicleRecommendations && (
            <Card className="border-emerald-200">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Truck className="w-5 h-5 text-emerald-600" />
                  <div>
                    <CardTitle>Fleet Vehicle Recommendations</CardTitle>
                    <CardDescription>
                      Evaluated against registered transport vehicles, payload limits, and interior cargo bay dimensions
                    </CardDescription>
                  </div>
                </div>
              </CardHeader>

              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  {analysisData.vehicleRecommendations.recommendations.map((v) => {
                    const isSelected = selectedVehicle?.vehicleId === v.vehicleId;
                    const isBest = v.suitabilityRank === 'BestMatch';

                    return (
                      <div
                        key={v.vehicleId}
                        onClick={() => v.isSuitable && setSelectedVehicle(v)}
                        className={cn(
                          'p-4 rounded-xl border transition-all cursor-pointer relative',
                          isSelected
                            ? 'border-brand ring-2 ring-brand/20 bg-brand/5 shadow-sm'
                            : v.isSuitable
                            ? 'border-neutral-border bg-white hover:border-brand/50'
                            : 'border-neutral-border bg-neutral-appBg/50 opacity-60 cursor-not-allowed'
                        )}
                      >
                        <div className="flex items-start justify-between">
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="font-bold text-neutral-primary text-sm">
                                {v.type} ({v.plateNumber})
                              </span>
                              {isBest && (
                                <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-300">
                                  ★ Best Match
                                </span>
                              )}
                              {!v.isSuitable && (
                                <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-100 text-red-800 border border-red-200">
                                  {v.suitabilityRank}
                                </span>
                              )}
                            </div>
                            <p className="text-xs text-neutral-secondary mt-1">
                              Max Payload: {v.maxWeightKg.toLocaleString()} kg · Cargo Volume: {v.maxVolumeM3} m³
                            </p>
                          </div>

                          <div className="w-5 h-5 rounded-full border flex items-center justify-center transition-colors">
                            {isSelected && <div className="w-2.5 h-2.5 rounded-full bg-brand" />}
                          </div>
                        </div>

                        {/* Capacity Utilization Progress Bars */}
                        <div className="grid grid-cols-2 gap-3 mt-3 pt-3 border-t border-neutral-border/60 text-xs">
                          <div>
                            <div className="flex justify-between text-[11px] mb-1">
                              <span className="text-neutral-secondary">Weight Util.</span>
                              <span className="font-bold text-neutral-primary">{v.weightUtilizationPercentage}%</span>
                            </div>
                            <div className="w-full h-1.5 rounded-full bg-neutral-200 overflow-hidden">
                              <div
                                className={cn(
                                  'h-full rounded-full transition-all',
                                  v.weightUtilizationPercentage > 100
                                    ? 'bg-red-600'
                                    : v.weightUtilizationPercentage > 85
                                    ? 'bg-amber-500'
                                    : 'bg-emerald-500'
                                )}
                                style={{ width: `${Math.min(100, v.weightUtilizationPercentage)}%` }}
                              />
                            </div>
                          </div>

                          <div>
                            <div className="flex justify-between text-[11px] mb-1">
                              <span className="text-neutral-secondary">Volume Util.</span>
                              <span className="font-bold text-neutral-primary">{v.volumeUtilizationPercentage}%</span>
                            </div>
                            <div className="w-full h-1.5 rounded-full bg-neutral-200 overflow-hidden">
                              <div
                                className={cn(
                                  'h-full rounded-full transition-all',
                                  v.volumeUtilizationPercentage > 100
                                    ? 'bg-red-600'
                                    : v.volumeUtilizationPercentage > 85
                                    ? 'bg-amber-500'
                                    : 'bg-brand'
                                )}
                                style={{ width: `${Math.min(100, v.volumeUtilizationPercentage)}%` }}
                              />
                            </div>
                          </div>
                        </div>

                        {/* Reason / Warnings */}
                        <p className="text-[11px] text-neutral-secondary mt-2.5">
                          {v.reason}
                        </p>

                        {v.warnings.length > 0 && (
                          <div className="mt-2 space-y-1">
                            {v.warnings.map((w, wIdx) => (
                              <p key={wIdx} className="text-[10px] text-amber-700 flex items-center gap-1 font-medium">
                                <AlertCircle className="w-3 h-3 flex-shrink-0" />
                                {w}
                              </p>
                            ))}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              </CardContent>
            </Card>
          )}

          {/* Action Bar */}
          <div className="flex flex-col sm:flex-row items-center justify-between gap-3 p-4 rounded-xl bg-emerald-50 border border-emerald-200">
            <div className="flex items-center gap-2 text-xs text-emerald-900">
              <CheckCircle2 className="w-4 h-4 text-emerald-600 flex-shrink-0" />
              <span>
                Ready to apply: <strong>{analysisData.calculation.totalWeightKg} kg</strong>,{' '}
                <strong>{analysisData.calculation.totalPackedVolumeM3} m³</strong>
                {selectedVehicle && (
                  <>
                    {' '}
                    with recommended vehicle <strong>{selectedVehicle.type} ({selectedVehicle.plateNumber})</strong>
                  </>
                )}
              </span>
            </div>

            <Button
              type="button"
              variant="primary"
              size="md"
              onClick={handleConfirmAndApply}
              isLoading={confirmMutation.isPending}
              rightIcon={<ChevronRight className="w-4 h-4" />}
              className="w-full sm:w-auto bg-emerald-700 hover:bg-emerald-600 text-white shadow-md"
            >
              Confirm &amp; Populate Shipment
            </Button>
          </div>
        </div>
      )}

      {/* Edit / Add Item Modal */}
      {editingItemForm && (
        <Modal
          isOpen={isEditingItem}
          onClose={() => setIsEditingItem(false)}
          title={editingItemIndex !== null ? 'Edit Cargo Item' : 'Add Item to Cargo Inventory'}
          description="Adjust dimensions, weight, and disassembly parameters"
          maxWidth="lg"
        >
          <div className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div className="sm:col-span-2">
                <FormField label="Item Name" required>
                  <Input
                    value={editingItemForm.name}
                    onChange={(e) =>
                      setEditingItemForm({ ...editingItemForm, name: e.target.value })
                    }
                    placeholder="e.g. Master Bedroom Wardrobe"
                  />
                </FormField>
              </div>

              <FormField label="Category" required>
                <select
                  value={editingItemForm.category}
                  onChange={(e) =>
                    setEditingItemForm({
                      ...editingItemForm,
                      category: e.target.value as CargoCategory,
                    })
                  }
                  className="w-full h-10 px-3 rounded-lg border border-neutral-border text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand"
                >
                  {CATEGORIES.map((cat) => (
                    <option key={cat} value={cat}>
                      {cat}
                    </option>
                  ))}
                </select>
              </FormField>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <FormField label="Quantity" required>
                <Input
                  type="number"
                  min="1"
                  value={editingItemForm.quantity}
                  onChange={(e) =>
                    setEditingItemForm({
                      ...editingItemForm,
                      quantity: Math.max(1, parseInt(e.target.value) || 1),
                    })
                  }
                />
              </FormField>

              <FormField label="Length (cm)" required>
                <Input
                  type="number"
                  step="0.5"
                  value={editingItemForm.lengthCm}
                  onChange={(e) => {
                    const l = parseFloat(e.target.value) || 0;
                    setEditingItemForm({
                      ...editingItemForm,
                      lengthCm: l,
                      packedLengthCm: editingItemForm.canBeDisassembled ? editingItemForm.packedLengthCm : l,
                    });
                  }}
                />
              </FormField>

              <FormField label="Width (cm)" required>
                <Input
                  type="number"
                  step="0.5"
                  value={editingItemForm.widthCm}
                  onChange={(e) => {
                    const w = parseFloat(e.target.value) || 0;
                    setEditingItemForm({
                      ...editingItemForm,
                      widthCm: w,
                      packedWidthCm: editingItemForm.canBeDisassembled ? editingItemForm.packedWidthCm : w,
                    });
                  }}
                />
              </FormField>

              <FormField label="Height (cm)" required>
                <Input
                  type="number"
                  step="0.5"
                  value={editingItemForm.heightCm}
                  onChange={(e) => {
                    const h = parseFloat(e.target.value) || 0;
                    setEditingItemForm({
                      ...editingItemForm,
                      heightCm: h,
                      packedHeightCm: editingItemForm.canBeDisassembled ? editingItemForm.packedHeightCm : h,
                    });
                  }}
                />
              </FormField>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <FormField label="Weight (kg)" required>
                <Input
                  type="number"
                  step="0.5"
                  value={editingItemForm.weightKg}
                  onChange={(e) => {
                    const wt = parseFloat(e.target.value) || 0;
                    setEditingItemForm({
                      ...editingItemForm,
                      weightKg: wt,
                      packedWeightKg: wt,
                    });
                  }}
                />
              </FormField>

              <FormField label="Fragility Level">
                <select
                  value={editingItemForm.fragility}
                  onChange={(e) =>
                    setEditingItemForm({
                      ...editingItemForm,
                      fragility: e.target.value as FragilityLevel,
                    })
                  }
                  className="w-full h-10 px-3 rounded-lg border border-neutral-border text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand"
                >
                  <option value="Low">Low Fragility</option>
                  <option value="Medium">Medium Fragility</option>
                  <option value="High">High Fragility (Glass/Delicate)</option>
                </select>
              </FormField>

              <FormField label="Packing Complexity">
                <select
                  value={editingItemForm.packingComplexity}
                  onChange={(e) =>
                    setEditingItemForm({
                      ...editingItemForm,
                      packingComplexity: e.target.value as PackingComplexity,
                    })
                  }
                  className="w-full h-10 px-3 rounded-lg border border-neutral-border text-sm bg-white focus:outline-none focus:ring-2 focus:ring-brand"
                >
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High (Professional Assembly)</option>
                </select>
              </FormField>
            </div>

            {/* Disassembly Section */}
            <div className="p-3.5 rounded-xl bg-neutral-appBg/60 border border-neutral-border space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <span className="text-xs font-bold text-neutral-primary block">
                    Can be disassembled for transport?
                  </span>
                  <span className="text-[11px] text-neutral-secondary">
                    Separating panels or detachable legs reduces required vehicle volume
                  </span>
                </div>
                <input
                  type="checkbox"
                  checked={editingItemForm.canBeDisassembled}
                  onChange={(e) =>
                    setEditingItemForm({
                      ...editingItemForm,
                      canBeDisassembled: e.target.checked,
                    })
                  }
                  className="w-4 h-4 rounded text-brand focus:ring-brand"
                />
              </div>

              {editingItemForm.canBeDisassembled && (
                <div className="space-y-3 pt-2 border-t border-neutral-border/60">
                  <div className="grid grid-cols-3 gap-2">
                    <FormField label="Packed L (cm)">
                      <Input
                        type="number"
                        value={editingItemForm.packedLengthCm}
                        onChange={(e) =>
                          setEditingItemForm({
                            ...editingItemForm,
                            packedLengthCm: parseFloat(e.target.value) || 0,
                          })
                        }
                      />
                    </FormField>
                    <FormField label="Packed W (cm)">
                      <Input
                        type="number"
                        value={editingItemForm.packedWidthCm}
                        onChange={(e) =>
                          setEditingItemForm({
                            ...editingItemForm,
                            packedWidthCm: parseFloat(e.target.value) || 0,
                          })
                        }
                      />
                    </FormField>
                    <FormField label="Packed H (cm)">
                      <Input
                        type="number"
                        value={editingItemForm.packedHeightCm}
                        onChange={(e) =>
                          setEditingItemForm({
                            ...editingItemForm,
                            packedHeightCm: parseFloat(e.target.value) || 0,
                          })
                        }
                      />
                    </FormField>
                  </div>

                  <FormField label="Disassembly Notes / Guidance">
                    <Input
                      value={editingItemForm.disassemblyNotes || ''}
                      onChange={(e) =>
                        setEditingItemForm({
                          ...editingItemForm,
                          disassemblyNotes: e.target.value,
                        })
                      }
                      placeholder="e.g. Legs unscrew; tabletop wrapped separately"
                    />
                  </FormField>
                </div>
              )}
            </div>

            <FormField label="Special Handling Instructions (Optional)">
              <Input
                value={editingItemForm.specialHandling || ''}
                onChange={(e) =>
                  setEditingItemForm({
                    ...editingItemForm,
                    specialHandling: e.target.value,
                  })
                }
                placeholder="e.g. Wrap in moving blankets, keep upright"
              />
            </FormField>

            <div className="flex justify-end gap-2 pt-3 border-t border-neutral-border">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setIsEditingItem(false)}
              >
                Cancel
              </Button>
              <Button
                type="button"
                variant="primary"
                size="sm"
                isLoading={updateMutation.isPending}
                onClick={saveItemChanges}
              >
                Save Changes
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
};
