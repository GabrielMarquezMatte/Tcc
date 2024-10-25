from arch.univariate.mean import HARX
from arch.univariate.base import ARCHModelResult
from arch import arch_model
import numpy as np
from typing import Literal
from itertools import product
from concurrent.futures import ProcessPoolExecutor
import datetime as dt

vol_models = Literal['GARCH', 'ARCH', 'EGARCH', 'FIGARCH', 'APARCH', 'HARCH']
mean_types = Literal['Constant', 'Zero', 'LS', 'AR', 'ARX', 'HAR', 'HARX', 'constant', 'zero']
distribution_types = Literal['normal', 'gaussian', 't', 'studentst', 'skewstudent', 'skewt', 'ged', 'generalized error']

array_type = np.ndarray[int,np.dtype[np.float64]]

def __fit_model(order: tuple[int,int,int], returns: array_type,
                volatility_model:vol_models,
                mean_model:mean_types,
                distribution:distribution_types):
    p, q, o = order
    model = arch_model(returns, p=p, q=q, vol= volatility_model, o = o, mean = mean_model,
                       dist=distribution)
    results = model.fit(update_freq=0, disp='off')
    return order, volatility_model, mean_model, distribution, results, model

def __fit_model_parallel(parameters: tuple[tuple[tuple[int,int,int], vol_models], array_type, mean_types, distribution_types]):
    (order, volatility_model), returns, mean_model, distribution = parameters
    return __fit_model(order, returns, volatility_model, mean_model, distribution)

def __create_orders(max_p:int=3, max_q:int=3, max_o:int=0, volatility_models:list[vol_models]|None=None):
    if max_p < 0 or max_q < 0 or max_o < 0:
        raise ValueError("All orders must be non-negative")
    if max_p == 0 and max_o == 0:
        raise ValueError("One of p or o must be strictly positive")
    if volatility_models is None:
        volatility_models = ['GARCH']
    for p, q, o, volatility_model in product(range(max_p+1), range(max_o+1), range(max_q+1), volatility_models):
        if p == 0 and o == 0:
            continue
        if (p == 0 or q == 0) and o > 0:
            continue
        if volatility_model == "APARCH" and o > p:
            continue
        if volatility_model == 'FIGARCH' and (p > 1 or q > 1):
            continue
        yield (p, q, o,), volatility_model

def FindBestGarch(executor:ProcessPoolExecutor, returns:array_type, max_p:int=3, max_q:int=3, max_o:int=0,
                  volatility_models:list[vol_models] = ["GARCH"], mean_models:list[mean_types] = ['Constant'],
                  distributions:list[distribution_types] = ['normal'], verbose:bool = False):
    if volatility_models is None:
        volatility_models = ['GARCH']
    if mean_models is None:
        mean_models = ['Constant']
    if distributions is None:
        distributions = ['normal']
    orders = __create_orders(max_p, max_q, max_o, volatility_models)
    all_models: dict[tuple[int|str, ...], tuple[ARCHModelResult, HARX]] = {}
    for order, volatility_model, mean_model, distribution, results, model in executor.map(__fit_model_parallel, product(orders, [returns], mean_models, distributions)):
        model_specs = tuple([*order, volatility_model, mean_model, distribution])
        all_models[model_specs] = (results, model)
        if verbose:
            print(f"Finished fitting model: {model_specs}")
    best_order = min(all_models, key=lambda order: all_models[order][0].bic)
    best_model = all_models[best_order][1]
    result = all_models[best_order][0]
    return all_models, best_order, best_model, result

if __name__ == "__main__":
    returns = np.random.normal(0, 1, 1000)
    start = dt.datetime.now()
    with ProcessPoolExecutor(max_workers=4) as executor:
        all_models, best_order, best_model, result = FindBestGarch(executor, returns, max_p=2, max_q=2, max_o=1,
                                                            volatility_models=['GARCH', 'ARCH', 'EGARCH', 'FIGARCH', 'APARCH', 'HARCH'],
                                                            distributions=['normal','t','skewt'],
                                                            mean_models=['AR', 'ARX', 'HAR', 'HARX','Constant'], verbose=True)
    end = dt.datetime.now()
    print(f"Fitted the best model ({best_order}) in {len(all_models)} models with BIC: {all_models[best_order][0].bic:.2f} in {end-start}")