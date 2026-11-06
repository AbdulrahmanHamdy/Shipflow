import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cargoAnalysisApi } from '@/api';
import {
  CargoAnalysisResponseDto,
  UpdateCargoItemInputDto,
  VehicleRecommendationResultDto,
} from '@/api/types';
import { queryKeys } from '@/lib/queryClient';

export function useCargoAnalysisQuery(id: string | undefined) {
  return useQuery<CargoAnalysisResponseDto>({
    queryKey: queryKeys.cargo.analysis(id || ''),
    queryFn: () => {
      if (!id) throw new Error('Analysis ID is required');
      return cargoAnalysisApi.getById(id);
    },
    enabled: !!id,
  });
}

export function useAnalyzeCargoMutation() {
  const queryClient = useQueryClient();
  return useMutation<CargoAnalysisResponseDto, unknown, File[]>({
    mutationFn: (images) => cargoAnalysisApi.analyze(images),
    onSuccess: (data) => {
      queryClient.setQueryData(queryKeys.cargo.analysis(data.sessionId), data);
    },
  });
}

export function useUpdateCargoItemsMutation() {
  const queryClient = useQueryClient();
  return useMutation<
    CargoAnalysisResponseDto,
    unknown,
    { id: string; items: UpdateCargoItemInputDto[] }
  >({
    mutationFn: ({ id, items }) => cargoAnalysisApi.updateItems(id, items),
    onSuccess: (data, { id }) => {
      queryClient.setQueryData(queryKeys.cargo.analysis(id), data);
    },
  });
}

export function useConfirmCargoMutation() {
  const queryClient = useQueryClient();
  return useMutation<CargoAnalysisResponseDto, unknown, string>({
    mutationFn: (id) => cargoAnalysisApi.confirm(id),
    onSuccess: (data, id) => {
      queryClient.setQueryData(queryKeys.cargo.analysis(id), data);
    },
  });
}

export function useRecommendVehiclesMutation() {
  return useMutation<
    VehicleRecommendationResultDto,
    unknown,
    UpdateCargoItemInputDto[]
  >({
    mutationFn: (items) => cargoAnalysisApi.recommendVehicles(items),
  });
}
