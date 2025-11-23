/**
 * Decorator to automatically track function calls
 * Usage: @TrackFunction('ComponentName')
 */
export function TrackFunction(componentName?: string) {
  return function (target: any, propertyKey: string, descriptor: PropertyDescriptor) {
    const originalMethod = descriptor.value;

    descriptor.value = function (...args: any[]) {
      const errorTrackingService = (this as any).errorTrackingService;
      
      if (errorTrackingService) {
        const startTime = performance.now();
        const functionName = `${target.constructor.name}.${propertyKey}`;
        
        try {
          const result = originalMethod.apply(this, args);
          
          // Handle promises
          if (result instanceof Promise) {
            return result
              .then((value) => {
                const duration = performance.now() - startTime;
                errorTrackingService.trackFunctionCall(
                  functionName,
                  componentName,
                  args,
                  value,
                  duration
                );
                return value;
              })
              .catch((error) => {
                errorTrackingService.trackError(functionName, error, componentName, {
                  arguments: args
                });
                throw error;
              });
          } else {
            // Synchronous function
            const duration = performance.now() - startTime;
            errorTrackingService.trackFunctionCall(
              functionName,
              componentName,
              args,
              result,
              duration
            );
            return result;
          }
        } catch (error) {
          errorTrackingService.trackError(functionName, error, componentName, {
            arguments: args
          });
          throw error;
        }
      } else {
        // No error tracking service available, just execute
        return originalMethod.apply(this, args);
      }
    };

    return descriptor;
  };
}



