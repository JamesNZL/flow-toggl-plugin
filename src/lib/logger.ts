// Winston file logger with clean output and write to file

import { createLogger, format, transports } from 'winston';

const logger = createLogger({
	level: 'info',
	format: format.combine(
		format.timestamp({ format: 'YYYY-MM-DD HH:mm:ss' }),
		// Format the metadata object
		format.metadata({ fillExcept: ['message', 'level', 'timestamp', 'label'] }),
	),
	transports: [
		new transports.File({
			filename: 'logs/flow.log',
			format: format.combine(
				// Render in one line in your log file.
				// If you use prettyPrint() here it will be really
				// difficult to exploit your logs files afterwards.
				format.json(),
			),
		}),
	],
	exitOnError: false,
});

export default logger;
