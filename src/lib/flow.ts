import { readFileSync } from 'fs';
import { join } from 'path';
import { cwd } from 'process';
import { Logger } from 'winston';
import { PLUGIN_MANIFEST } from './constants';
import logger from './logger';

type JSONRPCMethods =
  | 'Flow.Launcher.ChangeQuery'
  | 'Flow.Launcher.RestartApp'
  | 'Flow.Launcher.SaveAppAllSettings'
  | 'Flow.Launcher.CheckForNewUpdate'
  | 'Flow.Launcher.ShellRun'
  | 'Flow.Launcher.CloseApp'
  | 'Flow.Launcher.HideApp'
  | 'Flow.Launcher.ShowApp'
  | 'Flow.Launcher.ShowMsg'
  | 'Flow.Launcher.GetTranslation'
  | 'Flow.Launcher.OpenSettingDialog'
  | 'Flow.Launcher.GetAllPlugins'
  | 'Flow.Launcher.StartLoadingBar'
  | 'Flow.Launcher.StopLoadingBar'
  | 'Flow.Launcher.ReloadAllPluginData'
  | 'Flow.Launcher.CopyToClipboard'
  | 'query'
  | 'context_menu';

type Methods<T> = JSONRPCMethods | T;

type MethodsObj<T> = {
  [key in Methods<T> extends string
    ? Methods<T>
    : // eslint-disable-next-line @typescript-eslint/ban-types
      JSONRPCMethods | (string & {})]: () => void;
};

type ParametersAllowedTypes =
  | string
  | number
  | boolean
  | Record<string, unknown>
  | ParametersAllowedTypes[];

type Method<T> = keyof MethodsObj<T>;
type Parameters = ParametersAllowedTypes[];

interface Data<TMethods, TSettings> {
  method: Method<TMethods>;
  parameters: Parameters;
  settings: TSettings;
}

export interface PluginManifest {
  ID: string;
  ActionKeyword: string;
  Name: string;
  Description: string;
  Author: string;
  Version: string;
  Language: string;
  ExecuteFileName: string;
  Website: string;
  IcoPath: string;
}

export interface JSONRPCResponse<TMethods> {
  title: string;
  subtitle?: string;
  method?: Method<TMethods>;
  parameters?: Parameters;
  dontHideAfterAction?: boolean;
  iconPath?: string;
  score?: number;
  context?: Parameters;
}

interface IFlow<TMethods, TSettings> {
  settings: TSettings;
  on: (method: Method<TMethods>, callbackFn: () => void) => void;
  showResult: (...result: JSONRPCResponse<TMethods>[]) => void;
  run: () => void;
}

export class Flow<TMethods, TSettings = Record<string, string>>
implements IFlow<TMethods, TSettings> {
	private methods = {} as MethodsObj<TMethods>;
	private defaultIconPath: string | undefined;
	private readonly data: Data<TMethods, TSettings> = JSON.parse(process.argv[2]);

	private _manifest?: PluginManifest;

	public log: Logger;

	public id = this.manifest.ID;
	public icon = this.manifest.IcoPath;
	public name = this.manifest.Name;
	public description = this.manifest.Description;
	public author = this.manifest.Author;
	public version = this.manifest.Version;

	/**
   * Creates an instance of Flow.
   */
	constructor(defaultIconPath?: string) {
		this.defaultIconPath = defaultIconPath;
		this.showResult = this.showResult.bind(this);
		this.run = this.run.bind(this);
		this.on = this.on.bind(this);

		this.log = logger;
	}

	get settings(): TSettings {
		return this.data.settings;
	}

	get manifest(): PluginManifest {
		if (this._manifest) {
			return this._manifest;
		}

		const path = join(cwd(), PLUGIN_MANIFEST);
		const file = readFileSync(path, 'utf8');
		return this._manifest = JSON.parse(file);
	}

	/**
   * Registers a method and the function that will run when this method is sent from Flow.
   */
	public on(method: keyof MethodsObj<TMethods>, callbackFn: (params: Parameters) => void) {
		this.methods[method] = callbackFn.bind(this, this.data.parameters);
	}

	/**
   * Sends the data to be displayed in Flow Launcher.
   */
	public showResult(...results: JSONRPCResponse<TMethods>[]) {
		const result = results.map(r => {
			return {
				Title: r.title,
				Subtitle: r.subtitle,
				JsonRPCAction: {
					method: r.method,
					parameters: r.parameters ?? [],
					dontHideAfterAction: r.dontHideAfterAction ?? false,
				},
				ContextData: r.context ?? [],
				IcoPath: r.iconPath ?? this.defaultIconPath,
				Score: r.score ?? 0,
			};
		});

		return console.log(JSON.stringify({ result }));
	}

	public changeQuery({ query, requery = false }: { query: string; requery?: boolean }) {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.ChangeQuery',
				parameters: [query, requery],
			}),
		);
	}

	public restartApp() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.RestartApp',
				parameters: [],
			}),
		);
	}

	public saveAppAllSettings() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.SaveAppAllSettings',
				parameters: [],
			}),
		);
	}

	public checkForNewUpdate() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.CheckForNewUpdate',
				parameters: [],
			}),
		);
	}

	public shellRun({ command }: { command: string }) {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.ShellRun',
				parameters: [command],
			}),
		);
	}

	public closeApp() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.CloseApp',
				parameters: [],
			}),
		);
	}

	public hideApp() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.HideApp',
				parameters: [],
			}),
		);
	}

	public showApp() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.ShowApp',
				parameters: [],
			}),
		);
	}

	public showMsg({ title, subTitle, icoPath }: {title: string, subTitle: string, icoPath: string}) {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.ShowMsg',
				parameters: [title, subTitle, icoPath],
			}),
		);
	}

	public openSettingDialog() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.OpenSettingDialog',
				parameters: [],
			}),
		);
	}

	public startLoadingBar() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.StartLoadingBar',
				parameters: [],
			}),
		);
	}

	public stopLoadingBar() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.StopLoadingBar',
				parameters: [],
			}),
		);
	}

	public reloadPlugins() {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.ReloadPlugins',
				parameters: [],
			}),
		);
	}

	public copyToClipboard({ text }: { text: string }) {
		console.log(
			JSON.stringify({
				method: 'Flow.Launcher.CopyToClipboard',
				parameters: [text],
			}),
		);
	}

	/**
   * Runs the function for the current method. Should be called at the end of your script, or after all the `on()` functions have been called.
   */
	public run() {
		this.data.method in this.methods && this.methods[this.data.method]();
	}
}
