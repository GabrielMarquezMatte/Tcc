import numpy as np
from qpsolvers import solve_qp # type: ignore
import datetime as dt

array_type = np.ndarray[int,np.dtype[np.float64]]

class PortfolioOptimization:
    def __init__(self, expected_returns:dict[str,float], covariance_matrix:array_type, risk_free_rate:float=0.0):
        self.expected_returns = expected_returns
        self.covariance_matrix = covariance_matrix
        self.risk_free_rate = risk_free_rate
        self.__array_expected_returns = np.array(list(expected_returns.values()))
        self.__number_of_assets = len(expected_returns)
    
    @staticmethod
    def __calculate_portfolio_return(weights:array_type, expected_returns: array_type) -> float:
        return np.dot(weights, expected_returns)
    
    @staticmethod
    def __calculate_portfolio_variance(weights:array_type, covariance_matrix:array_type) -> float:
        return np.dot(weights.T, np.dot(covariance_matrix, weights))
    
    @staticmethod
    def __calculate_portfolio_risk(weights:array_type, covariance_matrix:array_type) -> float:
        return np.sqrt(PortfolioOptimization.__calculate_portfolio_variance(weights, covariance_matrix))
    
    @staticmethod
    def __calculate_portfolio_sharpe_ratio(weights:array_type, expected_returns:array_type, covariance_matrix:array_type, risk_free_rate:float) -> float:
        adjusted_return = PortfolioOptimization.__calculate_portfolio_return(weights, expected_returns) - risk_free_rate
        return adjusted_return / PortfolioOptimization.__calculate_portfolio_risk(weights, covariance_matrix)
    
    @staticmethod
    def __add_column(matrix:array_type, value:float=1.0):
        column_index = matrix.shape[1]
        return np.insert(matrix, column_index, value, axis=1)
    
    @staticmethod
    def __add_column_values(matrix:array_type, values:array_type):
        column_index = matrix.shape[1]
        return np.insert(matrix, column_index, values, axis=1)
    
    @staticmethod
    def __add_row(matrix:array_type, value:float=1.0):
        row_index = matrix.shape[0]
        return np.insert(matrix, row_index, value, axis=0)
    
    @staticmethod
    def __add_row_values(matrix:array_type, values:array_type):
        row_index = matrix.shape[0]
        return np.insert(matrix, row_index, values, axis=0)
    
    def __risk_free_matrix(self):
        covariance = self.covariance_matrix
        expected_returns = self.__array_expected_returns
        covariance = self.__add_column(covariance,0)
        covariance = self.__add_row(covariance,0)
        covariance[self.__number_of_assets,self.__number_of_assets] = 1e-8
        n_values = self.__number_of_assets + 1
        expected_returns = np.append(expected_returns, self.risk_free_rate)
        return covariance, expected_returns, n_values
    
    def target_return_portfolio(self, target_return:float, use_risk_free:bool = False, short:bool = True,
                                min_weight:float = 0.0, max_weight:float = 1.0):
        covariance = self.covariance_matrix
        n_values = self.__number_of_assets
        expected_returns = self.__array_expected_returns
        if use_risk_free:
            covariance, expected_returns, n_values = self.__risk_free_matrix()
        dmat = 2*covariance
        dvec = np.zeros(n_values)
        if short:
            ones = np.ones(n_values)
            dmat = self.__add_row_values(dmat, expected_returns)
            dmat = self.__add_row_values(dmat, ones)
            rets = np.append(expected_returns, [0,0])
            dmat = self.__add_column_values(dmat, rets)
            ones = np.append(ones, [0, 0])
            dmat = self.__add_column_values(dmat, ones)
            bvec = np.zeros(n_values)
            bvec = np.append(bvec, [target_return, 1])
            sol = np.linalg.solve(dmat, bvec)
            weights = sol[0:n_values]
        else:
            p = dmat
            q = dvec
            a = np.concatenate([np.ones(n_values), expected_returns]).reshape(2,n_values)
            lb = np.array([min_weight]*n_values)
            ub = np.array([max_weight]*n_values)
            b = np.array([1.0, target_return])
            sol:array_type|None = solve_qp(P = p, q = q, lb = lb, ub = ub, A = a, b = b, solver="daqp",verbose=True) # type: ignore
            if sol is None:
                raise ValueError(f"No solution found for {target_return} target return")
            weights = sol/np.sum(sol)
        dict_return = {
            "weights": weights,
            "return": self.__calculate_portfolio_return(weights, expected_returns),
            "risk": self.__calculate_portfolio_risk(weights, covariance),
            "sharpe_ratio": self.__calculate_portfolio_sharpe_ratio(weights, expected_returns, covariance, self.risk_free_rate)
        }
        return dict_return
    
    def min_variance_portfolio(self, short:bool = True, min_weight:float = 0.0, max_weight:float = 1.0):
        covariance = self.covariance_matrix
        n_values = self.__number_of_assets
        expected_returns = self.__array_expected_returns
        if short:
            dmat = 2*covariance
            dvec = np.zeros(n_values+1)
            dvec[n_values] = 1
            dmat = self.__add_column(dmat,1)
            dmat = self.__add_row(dmat,1)
            dmat[n_values,n_values] = 0
            sol = np.linalg.solve(dmat, dvec)
            weights = sol[0:n_values]
        else:
            p = 2*covariance
            q = np.zeros(n_values)
            a = np.ones(n_values).reshape(1,n_values)
            lb = np.array([min_weight]*n_values)
            ub = np.array([max_weight]*n_values)
            b = np.array([1.0])
            sol:array_type|None = solve_qp(P = p, q = q, lb = lb, ub = ub, A = a, b = b, solver="daqp") # type: ignore
            if sol is None:
                raise ValueError("No solution found for min variance portfolio")
            weights = sol/np.sum(sol)
        dict_return = {
            "weights": weights,
            "return": self.__calculate_portfolio_return(weights, expected_returns),
            "risk": self.__calculate_portfolio_risk(weights, covariance),
            "sharpe_ratio": self.__calculate_portfolio_sharpe_ratio(weights, expected_returns, covariance, self.risk_free_rate)
        }
        return dict_return
    
    def maximum_sharpe(self, short: bool = True, min_weight: float = 0, max_weight: float = 1):
        covariance = self.covariance_matrix
        n_values = self.__number_of_assets
        expected_returns = self.__array_expected_returns
        excedent_returns = expected_returns - self.risk_free_rate
        if short:
            dmat = 2*covariance
            #Add the excedent returns to the last row and column
            dmat = np.insert(dmat, n_values, excedent_returns, axis=0)
            excedent_returns = np.append(excedent_returns, 0)
            dmat = np.insert(dmat, n_values, excedent_returns, axis=1)
            dvec = np.zeros(n_values+1)
            dvec[n_values] = 1
            sol = np.linalg.solve(dmat, dvec)
            weights = sol[0:n_values]/np.sum(sol[0:n_values])
        else:
            p = 2*covariance
            q = np.zeros(n_values)
            a = excedent_returns.reshape(1,n_values)
            lb = np.array([min_weight]*n_values)
            ub = np.array([max_weight]*n_values)
            b = np.array([1.0])
            sol:array_type|None = solve_qp(P = p, q = q, lb = lb, ub = ub, A = a, b = b, solver="daqp") # type: ignore
            if sol is None:
                raise ValueError("No solution found for maximum sharpe portfolio")
            weights = sol
        dict_return = {
            "weights": weights,
            "return": self.__calculate_portfolio_return(weights, expected_returns),
            "risk": self.__calculate_portfolio_risk(weights, covariance),
            "sharpe_ratio": self.__calculate_portfolio_sharpe_ratio(weights, expected_returns, covariance, self.risk_free_rate)
        }
        return dict_return
    
    def maximum_utility(self, delta:float = 5, use_risk_free: bool = False, short: bool = True, min_weight: float = 0, max_weight: float = 1):
        covariance = self.covariance_matrix
        n_values = self.__number_of_assets
        expected_returns = self.__array_expected_returns
        if use_risk_free:
            covariance, expected_returns, n_values = self.__risk_free_matrix()
        if short:
            dmat = delta*covariance
            dmat = np.insert(dmat, n_values, 1, axis=0)
            dmat = np.insert(dmat, n_values, 1, axis=1)
            dmat[n_values,n_values] = 0
            dvec = np.append(expected_returns, 1)
            sol = np.linalg.solve(dmat, dvec)
            weights = sol[0:n_values]/np.sum(sol[0:n_values])
        else:
            p = delta*covariance
            q = expected_returns
            a = np.ones(n_values).reshape(1,n_values)
            lb = np.array([min_weight]*n_values)
            ub = np.array([max_weight]*n_values)
            b = np.array([1.0])
            sol:array_type|None = solve_qp(P = p, q = q, lb = lb, ub = ub, A = a, b = b, solver="daqp")
            if sol is None:
                raise ValueError("No solution found for maximum utility portfolio")
            weights = sol
        dict_return = {
            "weights": weights,
            "return": self.__calculate_portfolio_return(weights, expected_returns),
            "risk": self.__calculate_portfolio_risk(weights, covariance),
            "sharpe_ratio": self.__calculate_portfolio_sharpe_ratio(weights, expected_returns, covariance, self.risk_free_rate)
        }
        return dict_return

if __name__ == "__main__":
    expected_returns = {
        "AAPL": 0.2,
        "MSFT": 0.3,
        "GOOG": 0.25,
    }
    covariance_matrix = np.array([
        [0.2, 0.12, 0.1],
        [0.12, 0.3, 0.15],
        [0.1, 0.15, 0.3]
    ])
    portfolio_optimization = PortfolioOptimization(expected_returns, covariance_matrix, 0.05)
    start = dt.datetime.now()
    portfolio_optimization.maximum_utility(short=True)
    portfolio_optimization.maximum_utility(short=False)
    end = dt.datetime.now()
    print(end-start)