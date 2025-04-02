using Playnite.SDK;
using System;
using System.Collections.Generic;

namespace EmuLibrary.RomTypes.PcInstaller.Handlers
{
    /// <summary>
    /// Factory for creating the appropriate archive handler based on file type
    /// </summary>
    public class ArchiveHandlerFactory
    {
        private readonly ILogger _logger;
        private readonly List<IArchiveHandler> _handlers = new List<IArchiveHandler>();

        public ArchiveHandlerFactory(ILogger logger)
        {
            _logger = logger;
            
            // Register all supported handlers
            _handlers.Add(new MultiRarHandler(logger));
            _handlers.Add(new IsoHandler(logger));
            
            // Add more handlers here as needed
        }

        /// <summary>
        /// Gets the appropriate handler for the specified file
        /// </summary>
        public IArchiveHandler GetHandler(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            try
            {
                foreach (var handler in _handlers)
                {
                    if (handler.CanHandle(filePath))
                        return handler;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error getting handler for file: {filePath}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the specified file can be handled by any registered handler
        /// </summary>
        public bool CanHandleFile(string filePath)
        {
            return GetHandler(filePath) != null;
        }
    }
}