import { Flow } from './lib/flow';
import { z } from 'zod';

// The events are the custom events that you define in the flow.on() method.
const events = ['search'] as const;
type Events = (typeof events)[number];

const flow = new Flow<Events>('https://cdn.jsdelivr.net/gh/JamesNZL/flow-toggl-plugin@main/assets/app.png');

flow.on('query', (args) => {
	const [query] = z.array(z.string()).parse(args);

	flow.showResult(
		{
			title: 'Start time entry',
			subtitle: query,
		// method: 'search',
		// parameters: [],
		},
		{
			title: 'Stop time entry',
			subtitle: '0:52:43 Flow Launcher Toggl plugin',
		// method: 'search',
		// parameters: [],
		},
	);
});

flow.run();
